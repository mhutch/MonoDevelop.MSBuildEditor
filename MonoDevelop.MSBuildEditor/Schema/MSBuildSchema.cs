// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MonoDevelop.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MonoDevelop.MSBuildEditor.Schema
{
	class MSBuildSchema : IMSBuildSchema
	{
		public Dictionary<string, PropertyInfo> Properties { get; } = new Dictionary<string, PropertyInfo> (StringComparer.OrdinalIgnoreCase);
		public Dictionary<string, ItemInfo> Items { get; } = new Dictionary<string, ItemInfo> (StringComparer.OrdinalIgnoreCase);
		public Dictionary<string, TaskInfo> Tasks { get; } = new Dictionary<string, TaskInfo> (StringComparer.OrdinalIgnoreCase);
		public Dictionary<string, TargetInfo> Targets { get; } = new Dictionary<string, TargetInfo> (StringComparer.OrdinalIgnoreCase);
		public List<string> IntelliSenseImports { get; } = new List<string> ();

		public static MSBuildSchema Load (TextReader reader)
		{
			var schema = new MSBuildSchema ();
			schema.LoadInternal (reader);
			return schema;
		}

		public static MSBuildSchema LoadResource (string resourceId)
		{
			var asm = Assembly.GetCallingAssembly ();
			using (var sr = new StreamReader (asm.GetManifestResourceStream (resourceId))) {
				return Load (sr);
			}
		}

		void LoadInternal (TextReader reader)
		{
			JObject doc;
			using (var jr = new JsonTextReader (reader)) {
				doc = JObject.Load (jr);
			}

			foreach (var kv in doc) {
				switch (kv.Key) {
				case "properties":
					LoadProperties ((JObject)kv.Value);
					break;
				case "items":
					LoadItems ((JObject)kv.Value);
					break;
				case "targets":
					LoadTargets ((JObject)kv.Value);
					break;
				case "license":
					break;
				case "intellisenseImports":
					LoadIntelliSenseImports ((JArray)kv.Value);
					break;
				default:
					throw new Exception ($"Unknown property {kv.Key} in root");
				}
			}
		}

		void LoadProperties (JObject properties)
		{
			foreach (var kv in properties) {
				var name = kv.Key;
				if (kv.Value is JValue val) {
					Properties[name] = new PropertyInfo (name, (string)val.Value);
					continue;
				}
				string description = null, valueSeparators = null, defaultValue = null;
				var kind = MSBuildValueKind.Unknown;
				List<ConstantInfo> values = null;
				foreach (var pkv in (JObject)kv.Value) {
					switch (pkv.Key) {
					case "description":
						description = (string)pkv.Value;
						break;
					case "kind":
						kind = ParseValueKind ((string)((JValue)pkv.Value).Value);
						break;
					case "values":
						if (pkv.Value is JObject valuesObj) {
							values = GetValues ((JObject)pkv.Value);
						} else {
							values = GetValues ((JArray)pkv.Value);
						}
						break;
					case "default":
						defaultValue = (string)((JValue)pkv.Value).Value;
						break;
					case "valueSeparators":
						valueSeparators = (string)((JValue)pkv.Value).Value;
						break;
					default:
						throw new Exception ($"Unknown property {pkv.Key} in property {kv.Key}");
					}
				}

				kind = CheckKind (kind, valueSeparators, values);

				Properties[name] = new PropertyInfo (name, description, false, kind, values, defaultValue);
			}
		}

		MSBuildValueKind CheckKind (MSBuildValueKind kind, string valueSeparator, List<ConstantInfo> values)
		{
			if (kind == MSBuildValueKind.Unknown && values != null && values.Count > 0) {
				kind = MSBuildValueKind.String;
			}
			if (valueSeparator != null) {
				if (valueSeparator.IndexOf (',') > -1) {
					kind |= MSBuildValueKind.CommaList;
				}
				if (valueSeparator.IndexOf (';') > -1) {
					kind |= MSBuildValueKind.List;
				}
			}
			return kind;
		}

		void LoadItems (JObject items)
		{
			foreach (var kv in items) {
				var name = kv.Key;

				if (kv.Value is JValue val) {
					var s = (string)val.Value;
					if (!s.StartsWith ("@(", StringComparison.Ordinal)
						|| !s.EndsWith (")", StringComparison.Ordinal)
						|| !Items.TryGetValue (s.Substring (2, s.Length - 3), out ItemInfo refVal)
						) {
						throw new Exception ($"Invalid item reference '{s}' for item {name}");
					}
					var i = new ItemInfo (name, refVal.Description, refVal.IncludeDescription, refVal.ValueKind);
					//clone the metadata so we can parent it properly
					foreach (var m in refVal.Metadata.Values) {
						i.Metadata.Add (m.Name, new MetadataInfo (
							m.Name, m.Description, m.Reserved, m.Required,
							m.ValueKind, i, m.Values, m.DefaultValue)
						);
					}
					Items [name] = i;
					continue;
				}

				string description = null, includeDescription = null;
				var kind = MSBuildValueKind.Unknown;
				JObject metadata = null;
				foreach (var ikv in (JObject)kv.Value) {
					switch (ikv.Key) {
					case "description":
						description = (string)((JValue)ikv.Value).Value;
						break;
					case "kind":
						kind = ParseValueKind ((string)((JValue)ikv.Value).Value);
						break;
					case "includeDescription":
						includeDescription = (string)((JValue)ikv.Value).Value;
						break;
					case "metadata":
						metadata = (JObject)ikv.Value;
						break;
					default:
						throw new Exception ($"Unknown property {ikv.Key} in item {kv.Key}");
					}
				}
				var item = new ItemInfo (name, description, includeDescription, kind);
				if (metadata != null) {
					AddMetadata (item, metadata);
				}
				Items[name] = item;
			}
		}

		static MSBuildValueKind ParseValueKind (string valueKind)
		{
			var split = valueKind.Split ('-');

			if (!Enum.TryParse (split[0], true, out MSBuildValueKind result)) {
				//accept unknown values in case we run into newer schema formats
				LoggingService.LogDebug ($"Unknown value kind '{valueKind}'");
				return MSBuildValueKind.Unknown;
			}

			//whitelist permitted values
			switch (result) {
			case MSBuildValueKind.Data:
			case MSBuildValueKind.Bool:
			case MSBuildValueKind.Int:
			case MSBuildValueKind.String:
			case MSBuildValueKind.Guid:
			case MSBuildValueKind.Url:
			case MSBuildValueKind.Version:
			case MSBuildValueKind.SuffixedVersion:
			case MSBuildValueKind.Lcid:
			case MSBuildValueKind.TargetName:
			case MSBuildValueKind.ItemName:
			case MSBuildValueKind.PropertyName:
			case MSBuildValueKind.Sdk:
			case MSBuildValueKind.SdkVersion:
			case MSBuildValueKind.Label:
			case MSBuildValueKind.Importance:
			case MSBuildValueKind.RuntimeID:
			case MSBuildValueKind.TargetFramework:
			case MSBuildValueKind.TargetFrameworkVersion:
			case MSBuildValueKind.TargetFrameworkIdentifier:
			case MSBuildValueKind.TargetFrameworkProfile:
			case MSBuildValueKind.TargetFrameworkMoniker:
			case MSBuildValueKind.NuGetID:
			case MSBuildValueKind.NuGetVersion:
			case MSBuildValueKind.ProjectFile:
			case MSBuildValueKind.File:
			case MSBuildValueKind.Folder:
			case MSBuildValueKind.FolderWithSlash:
			case MSBuildValueKind.FileOrFolder:
			case MSBuildValueKind.Extension:
			case MSBuildValueKind.Configuration:
			case MSBuildValueKind.Platform:
			case MSBuildValueKind.ProjectKindGuid:
				break;
			default:
				LoggingService.LogDebug ($"Value '{result}' not permitted in schema");
				return MSBuildValueKind.Unknown;
			}

			for (int i = 1; i < split.Length; i++) {
				switch (split[i]) {
				case "list":
					result = result.List ();
					continue;
				case "const":
					result = result.Literal ();
					continue;
				default:
					LoggingService.LogDebug ($"Unknown value suffix '{split[i]}'");
					continue;
				}
			}
			return result;
		}

		MetadataInfo GetMetadataReference (string desc, ItemInfo parent)
		{
			if (!desc.StartsWith ("%(", StringComparison.Ordinal) || !desc.EndsWith (")", StringComparison.Ordinal)) {
				return null;
			}
			var split = desc.Substring (2, desc.Length - 3).Split ('.');
			if (split.Length == 1 && parent != null && parent.Metadata.TryGetValue (split[0], out MetadataInfo sibling)) {
				return sibling;
			}
			if (split.Length == 2 && Items.TryGetValue (split[0], out ItemInfo item) && item.Metadata.TryGetValue (split[1], out MetadataInfo cousin)) {
				return cousin;
			}
			throw new Exception ($"Invalid metadata reference {desc} in item {parent.Name}");
		}

		MetadataInfo WithNewName (MetadataInfo meta, string name, ItemInfo item)
		{
			return new MetadataInfo (
				name, meta.Description, meta.Reserved, meta.Required,
				meta.ValueKind, item, meta.Values, meta.DefaultValue);
		}

		void AddMetadata (ItemInfo item, JObject metaObj)
		{
			foreach (var kv in metaObj) {
				var name = kv.Key;

				//simple version, just a description string
				if (kv.Value is JValue value) {
					var desc = ((string)value.Value).Trim ();
					var reference = GetMetadataReference (desc, item);
					if (reference != null) {
						item.Metadata.Add (name, WithNewName (reference, name, item));
					} else {
						item.Metadata.Add (name, new MetadataInfo (name, desc));
					}
					continue;
				}

				string description = null, valueSeparators = null, defaultValue = null;
				bool required = false;
				MSBuildValueKind kind = MSBuildValueKind.Unknown;
				List<ConstantInfo> values = null;
				foreach (var mkv in (JObject)kv.Value) {
					switch (mkv.Key) {
					case "description":
						description = (string)((JValue)mkv.Value).Value;
						break;
					case "kind":
						kind = ParseValueKind ((string)((JValue)mkv.Value).Value);
						break;
					case "values":
						switch (mkv.Value) {
						case JValue jv:
							var metaRef = GetMetadataReference ((string)jv.Value, item);
							if (metaRef == null) {
								throw new Exception ("Invalid metadata reference");
							}
							values = metaRef.Values;
							break;
						case JObject jo:
							values = GetValues (jo);
							break;
						}
						break;
					case "valueSeparators":
						valueSeparators = (string)((JValue)mkv.Value).Value;
						break;
					case "default":
						defaultValue = (string)((JValue)mkv.Value).Value;
						break;
					case "required":
						required = (bool)((JValue)mkv.Value).Value;
						break;
					default:
						throw new Exception ($"Unknown property {mkv.Key} in metadata {kv.Key}");
					}
				}

				kind = CheckKind (kind, valueSeparators, values);

				item.Metadata.Add (
					name,
					new MetadataInfo (
						name, description, false, required, kind, item,
						values, defaultValue
					)
				);
			}
		}

		List<ConstantInfo> GetValues (JObject value)
		{
			var values = new List<ConstantInfo> ();
			foreach (var ikv in value) {
				values.Add (new ConstantInfo (ikv.Key, (string)((JValue)ikv.Value).Value));
			}
			return values;
		}

		List<ConstantInfo> GetValues (JArray arr)
		{
			var values = new List<ConstantInfo> ();
			foreach (var val in arr) {
				values.Add (new ConstantInfo ((string)((JValue)val).Value, null));
			}
			return values;
		}

		public bool IsPrivate (string name)
		{
			//assembly everything in a schema is public
			return false;
		}

		void LoadTargets (JObject items)
		{
			foreach (var kv in items) {
				var name = kv.Key;
				var desc = (string)((JValue)kv.Value).Value;
				Targets.Add (name, new TargetInfo (name, desc));
			}
		}

		void LoadIntelliSenseImports (JArray intelliSenseImports)
		{
			foreach (var import in intelliSenseImports) {
				string val = (string)((JValue)import).Value;
				if (!string.IsNullOrEmpty(val)) {
					IntelliSenseImports.Add (val);
				}
			}
		}
	}
}