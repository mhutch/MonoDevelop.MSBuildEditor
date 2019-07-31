// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using MonoDevelop.Xml.Parser;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MonoDevelop.MSBuild.Schema
{
	class MSBuildSchema : IMSBuildSchema, IEnumerable<BaseInfo>
	{
		public Dictionary<string, PropertyInfo> Properties { get; } = new Dictionary<string, PropertyInfo> (StringComparer.OrdinalIgnoreCase);
		public Dictionary<string, ItemInfo> Items { get; } = new Dictionary<string, ItemInfo> (StringComparer.OrdinalIgnoreCase);
		public Dictionary<string, TaskInfo> Tasks { get; } = new Dictionary<string, TaskInfo> (StringComparer.OrdinalIgnoreCase);
		public Dictionary<string, TargetInfo> Targets { get; } = new Dictionary<string, TargetInfo> (StringComparer.OrdinalIgnoreCase);
		public List<string> IntelliSenseImports { get; } = new List<string> ();

		public static MSBuildSchema Load (TextReader reader, out IList<(string, DiagnosticSeverity)> loadErrors)
		{
			var schema = new MSBuildSchema ();
			schema.LoadInternal (reader, out loadErrors);
			return schema;
		}

		public static MSBuildSchema LoadResource (string resourceId, out IList<(string, DiagnosticSeverity)> loadErrors)
		{
			var asm = Assembly.GetCallingAssembly ();
			using (var sr = new StreamReader (asm.GetManifestResourceStream (resourceId))) {
				return Load (sr, out loadErrors);
			}
		}

		class SchemaLoadState
		{
			public Dictionary<string, List<ConstantInfo>> CustomEnumKinds;
			public IList<(string, DiagnosticSeverity)> Errors;
			void AddError (string message, DiagnosticSeverity severity) => (Errors ?? (Errors = new List<(string, DiagnosticSeverity)> ())).Add ((message, severity));
			public void AddError (string error) => AddError (error, DiagnosticSeverity.Error);
			public void AddWarning (string error) => AddError (error, DiagnosticSeverity.Warning);
		}

		void LoadInternal (TextReader reader, out IList<(string, DiagnosticSeverity)> loadErrors)
		{
			var state = new SchemaLoadState ();

			JObject doc;
			using (var jr = new JsonTextReader (reader)) {
				doc = JObject.Load (jr);
			}

			JObject properties = null;
			JObject items = null;
			JObject targets = null;
			JArray intellisenseImports = null;
			JArray metadataGroups = null;
			JObject enumKinds = null;

			// we don't process the values in the switch, as we need a particular ordering
			foreach (var kv in doc) {
				switch (kv.Key) {
				case "properties":
					properties = (JObject)kv.Value;
					break;
				case "items":
					items = (JObject)kv.Value;
					break;
				case "targets":
					targets = (JObject)kv.Value;
					break;
				case "license":
				case "$schema":
					break;
				case "intellisenseImports":
					intellisenseImports = (JArray)kv.Value;
					break;
				case "metadata":
					metadataGroups = (JArray)kv.Value;
					break;
				case "enumKinds":
					enumKinds = (JObject)kv.Value;
					break;
				default:
					state.AddWarning ($"Unknown property {kv.Key} in root");
					break;
				}
			}

			if (intellisenseImports != null) {
				LoadIntelliSenseImports (intellisenseImports);
			}
			// enumKinds must come before properties, items and metadataGroups
			// as they may use the declared enum kinds
			if (enumKinds != null) {
				state.CustomEnumKinds = LoadEnumKinds (enumKinds, state);
			}
			if (properties != null) {
				LoadProperties (properties, state);
			}
			if (items != null) {
				LoadItems (items, state);
			}
			// metadataGroups must come after items, as it may apply metadata to existing items
			if (metadataGroups != null) {
				LoadMetadataGroups (metadataGroups, state);
			}
			if (targets != null) {
				LoadTargets (targets, state);
			}

			loadErrors = state.Errors ?? Array.Empty<(string, DiagnosticSeverity)> ();
		}

		void LoadProperties (JObject properties, SchemaLoadState state)
		{
			foreach (var kv in properties) {
				var name = kv.Key;
				if (kv.Value is JValue val) {
					Properties[name] = new PropertyInfo (name, (string)val.Value);
					continue;
				}
				string description = null, valueSeparators = null, defaultValue = null, deprecationMessage = null;
				bool deprecated = false;
				var kind = MSBuildValueKind.Unknown;
				List<ConstantInfo> values = null;
				foreach (var pkv in (JObject)kv.Value) {
					switch (pkv.Key) {
					case "description":
						description = (string)pkv.Value;
						break;
					case "kind":
						kind = ParseValueKind ((string)((JValue)pkv.Value).Value, ref values, state);
						break;
					case "values":
						values = LoadEnum (pkv.Value, state);
						break;
					case "default":
						defaultValue = (string)((JValue)pkv.Value).Value;
						break;
					case "valueSeparators":
						valueSeparators = (string)((JValue)pkv.Value).Value;
						break;
					case "deprecated":
						deprecated = (bool)((JValue)pkv.Value).Value;
						break;
					case "deprecationMessage":
						deprecationMessage = (string)((JValue)pkv.Value).Value;
						break;
					default:
						state.AddWarning ($"Unknown property {pkv.Key} in property {kv.Key}");
						break;
					}
				}

				kind = CheckKind (kind, valueSeparators, values);

				Properties[name] = new PropertyInfo (name, description, false, kind, values, defaultValue, deprecated, deprecationMessage);
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

		void LoadItems (JObject items, SchemaLoadState state)
		{
			foreach (var kv in items) {
				var name = kv.Key;

				string description = null, includeDescription = null, deprecationMessage = null;
				var kind = MSBuildValueKind.Unknown;
				JObject metadata = null;
				bool isDeprecated= false;
				foreach (var ikv in (JObject)kv.Value) {
					switch (ikv.Key) {
					case "description":
						description = (string)((JValue)ikv.Value).Value;
						break;
					case "kind":
						kind = ParseValueKind ((string)((JValue)ikv.Value).Value, out _, state);
						if (kind == MSBuildValueKind.CustomEnum) {
							state.AddError ($"Item '{name}' has custom enum value, which is not permitted for items");
						}
						break;
					case "includeDescription":
						includeDescription = (string)((JValue)ikv.Value).Value;
						break;
					case "metadata":
						metadata = (JObject)ikv.Value;
						break;
					case "deprecated":
						isDeprecated = (bool)((JValue)ikv.Value).Value;
						break;
					case "deprecationMessage":
						deprecationMessage = (string)((JValue)ikv.Value).Value;
						break;
					default:
						state.AddWarning ($"Unknown property {ikv.Key} in item {kv.Key}");
						break;
					}
				}
				var item = new ItemInfo (name, description, includeDescription, kind, null, isDeprecated, deprecationMessage);
				if (metadata != null) {
					AddMetadata (item, metadata, state);
				}
				Items[name] = item;
			}
		}

		MSBuildValueKind ParseValueKind (string valueKind, ref List<ConstantInfo> enumValues, SchemaLoadState state)
		{
			var kind = ParseValueKind (valueKind, out var enumName, state);
			if (kind == MSBuildValueKind.CustomEnum && enumValues == null) {
				if (state.CustomEnumKinds == null || !state.CustomEnumKinds.TryGetValue (enumName, out enumValues)) {
					state.AddError ($"Undefined custom enum '{enumName}'");
				}
			}
			return kind;
		}

		static MSBuildValueKind ParseValueKind (string valueKind, out string enumName, SchemaLoadState state)
		{
			var split = valueKind.Split ('-');

			if (split[0] == "enum") {
				enumName = split[1];
				return AddModifiers (MSBuildValueKind.CustomEnum, 2);
			}

			enumName = null;

			if (!Enum.TryParse (split[0], true, out MSBuildValueKind result)) {
				state.AddWarning ($"Unknown value kind '{valueKind}'");
				return MSBuildValueKind.Unknown;
			}


			//explicitly define permitted values
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
				return AddModifiers (result, 1);
			default:
				state.AddWarning ($"Value '{result}' not permitted in schema");
				return MSBuildValueKind.Unknown;
			}

			MSBuildValueKind AddModifiers (MSBuildValueKind kind, int modifiersIdx)
			{
				for (int i = modifiersIdx; i < split.Length; i++) {
					switch (split[i]) {
					case "list":
						kind = kind.List ();
						continue;
					case "const":
						kind = kind.Literal ();
						continue;
					default:
						state.AddWarning ($"Unknown value suffix '{split[i]}'");
						continue;
					}
				}
				return kind;
			}
		}

		MetadataInfo LoadMetadata (string name, JToken value, SchemaLoadState state)
		{
			//simple version, just a description string
			if (value is JValue v) {
				var desc = ((string)v.Value).Trim ();
				return new MetadataInfo (name, desc);
			}

			string description = null, valueSeparators = null, defaultValue = null, deprecationMessage = null;
			bool required = false;
			MSBuildValueKind kind = MSBuildValueKind.Unknown;
			List<ConstantInfo> values = null;
			bool isDeprecated = false;
			foreach (var mkv in (JObject)value) {
				switch (mkv.Key) {
				case "description":
					description = (string)((JValue)mkv.Value).Value;
					break;
				case "kind":
					kind = ParseValueKind ((string)((JValue)mkv.Value).Value, ref values, state);
					break;
				case "values":
					values = GetValues ((JObject)mkv.Value);
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
				case "deprecated":
					isDeprecated = (bool)((JValue)mkv.Value).Value;
					break;
				case "deprecationMessage":
					deprecationMessage = (string)((JValue)mkv.Value).Value;
					break;
				default:
					state.AddWarning ($"Unknown property {mkv.Key} in metadata {name}");
					break;
				}
			}

			kind = CheckKind (kind, valueSeparators, values);

			return new MetadataInfo (
				name, description, false, required, kind, null,
				values, defaultValue, isDeprecated, deprecationMessage
			);
		}

		void AddMetadata (ItemInfo item, JObject metaObj, SchemaLoadState state)
		{
			foreach (var kv in metaObj) {
				var name = kv.Key;
				var val = LoadMetadata (name, kv.Value, state);
				val.Item = item;
				item.Metadata.Add (name, val);
			}
		}

		static List<ConstantInfo> LoadEnum (JToken value, SchemaLoadState state)
		{
			if (value is JObject valuesObj) {
				return GetValues (valuesObj);
			}
			return GetValues ((JArray)value);
		}

		static List<ConstantInfo> GetValues (JObject value)
		{
			var values = new List<ConstantInfo> ();
			foreach (var ikv in value) {
				values.Add (new ConstantInfo (ikv.Key, (string)((JValue)ikv.Value).Value));
			}
			return values;
		}

		static List<ConstantInfo> GetValues (JArray arr)
		{
			var values = new List<ConstantInfo> ();
			foreach (var val in arr) {
				values.Add (new ConstantInfo ((string)((JValue)val).Value, null));
			}
			return values;
		}

		public bool IsPrivate (string name)
		{
			// everything in a schema is public
			return false;
		}

		void LoadTargets (JObject items, SchemaLoadState state)
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
				if (!string.IsNullOrEmpty (val)) {
					IntelliSenseImports.Add (val);
				}
			}
		}

		Dictionary<string, List<ConstantInfo>> LoadEnumKinds (JObject value, SchemaLoadState state)
		{
			var dict = new Dictionary<string, List<ConstantInfo>> ();
			foreach (var kv in value) {
				dict.Add (kv.Key, LoadEnum (kv.Value, state));
			}
			return dict;
		}

		void LoadMetadataGroups (JArray value, SchemaLoadState state)
		{
			foreach (var val in value) {
				LoadMetadataGroup ((JObject)val, state);
			}
		}

		void LoadMetadataGroup (JObject obj, SchemaLoadState state)
		{
			string[] appliesTo = null;
			var metadata = new List<MetadataInfo> ();

			foreach (var kv in obj) {
				//comments
				if (kv.Key[0] == '#') {
					continue;
				}
				if (kv.Key == "$appliesTo") {
					if (kv.Value is JArray arr) {
						appliesTo = new string[arr.Count];
						for(int i = 0; i< arr.Count; i++) {
							appliesTo[i] = (string)((JValue)arr[i]).Value;
						}
					} else {
						appliesTo = new[] { (string)kv.Value };
					}
					continue;
				}
				metadata.Add (LoadMetadata (kv.Key, kv.Value, state));
			}

			if (appliesTo == null) {
				state.AddError ("Metadata groups must have $appliesTo keys");
				return;
			}

			bool isFirstItem = true;

			foreach (var itemName in appliesTo) {
				if (!Items.TryGetValue (itemName, out ItemInfo item)) {
					item = new ItemInfo (itemName, null);
					Items.Add (itemName, item);
				}

				foreach (var m in metadata) {
					//the original metadata object gets parented on the first item, subsequent items get a copy
					var toAdd = isFirstItem ? m : new MetadataInfo (m.Name, m.Description, m.Reserved, m.Required, m.ValueKind, item, m.Values, m.DefaultValue);
					item.Metadata.Add (toAdd.Name, toAdd);
					toAdd.Item = item;
				}
				isFirstItem = false;
			}
		}

		IEnumerator<BaseInfo> IEnumerable<BaseInfo>.GetEnumerator ()
		{
			foreach (var item in Items.Values) {
				yield return item;
			}
			foreach (var prop in Properties.Values) {
				yield return prop;
			}
			foreach (var task in Tasks.Values) {
				yield return task;
			}
			foreach (var target in Targets.Values) {
				yield return target;
			}
		}

		IEnumerator IEnumerable.GetEnumerator () => ((IEnumerable<BaseInfo>)this).GetEnumerator ();

		public void Add (BaseInfo info)
		{
			switch (info) {
			case ItemInfo item:
				Items.Add (item.Name, item);
				break;
			case PropertyInfo prop:
				Properties.Add (prop.Name, prop);
				break;
			case TaskInfo task:
				Tasks.Add (task.Name, task);
				break;
			case TargetInfo target:
				Targets.Add (target.Name, target);
				break;
			default:
				throw new ArgumentException ($"Only items, properties, tasks and targets are allowed");
			}
		}
	}
}
