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
				case "license":
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
					Properties [name] = new PropertyInfo (name, (string)val.Value, false, false);
					continue;
				}
				string description = null, valueSeparator = null, defaultValue = null;
				var kind = MSBuildValueKind.PropertyExpression;
				List<ValueInfo> values = null;
				foreach (var pkv in (JObject)kv.Value) {
					switch (pkv.Key) {
					case "description":
						description = (string)pkv.Value;
						break;
					case "kind":
						kind = ParseValueKind ((string)((JValue)pkv.Value).Value) ?? kind;
						break;
					case "values":
						if (pkv.Value is JObject valuesObj) {
							values = GetValues ((JObject)pkv.Value);
						} else {
							values = GetValues ((JArray)pkv.Value);
						}
						break;
					case "defaultValue":
						defaultValue = (string)((JValue)pkv.Value).Value;
						break;
					case "valueSeparators":
						valueSeparator = (string)((JValue)pkv.Value).Value;
						break;
					default:
						throw new Exception ($"Unknown property {pkv.Key} in property {kv.Key}");
					}
				}
				Properties[name] = new PropertyInfo (name, description, false, false, kind, values, defaultValue, valueSeparator?.ToCharArray ());
			}
		}

		void LoadItems (JObject items)
		{
			foreach (var kv in items) {
				var name = kv.Key;
				string description = null, includeDescription = null;
				var kind = MSBuildItemKind.Unknown;
				Dictionary<string, MetadataInfo> metadata = null;
				foreach (var ikv in (JObject)kv.Value) {
					switch (ikv.Key) {
					case "description":
						description = (string)((JValue)ikv.Value).Value;
						break;
					case "kind":
						kind = ParseItemKind ((string)((JValue)ikv.Value).Value) ?? kind;
						break;
					case "includeDescription":
						includeDescription = (string)((JValue)ikv.Value).Value;
						break;
					case "metadata":
						metadata = GetMetadata (name, (JObject)ikv.Value);
						break;
					default:
						throw new Exception ($"Unknown property {ikv.Key} in item {kv.Key}");
					}
				}
				Items[name] = new ItemInfo (name, description, includeDescription, kind, metadata);
			}
		}

		static MSBuildItemKind? ParseItemKind (string itemKind)
		{
			//use explicit names instead of the enum to reduce breakable surface area
			switch (itemKind.ToLower ()) {
			case "file": return MSBuildItemKind.File;
			case "singlefile": return MSBuildItemKind.SingleFile;
			case "string": return MSBuildItemKind.String;
			case "singlestring": return MSBuildItemKind.SingleString;
			case "folder": return MSBuildItemKind.Folder;
			case "nugetid": return MSBuildItemKind.NuGetPackageID;
			case "url": return MSBuildItemKind.Url;
			default:
				//accept unknown values in case we run into newer schema formats
				LoggingService.LogDebug ($"Unknown item kind '{itemKind}'");
				return null;
			}
		}

		static MSBuildValueKind? ParseValueKind (string valueKind)
		{
			//use explicit names instead of the enum to reduce breakable surface area
			switch (valueKind.ToLower ()) {
			case "bool": return MSBuildValueKind.BoolExpression;
			case "targetframeworkversion": return MSBuildValueKind.TargetFrameworkVersion;
			case "importance": return MSBuildValueKind.Importance;
			case "file": return MSBuildValueKind.FileExpression;
			case "guid": return MSBuildValueKind.GuidExpression;
			case "int": return MSBuildValueKind.IntegerExpression;
			case "targetlist": return MSBuildValueKind.TargetListExpression;
			case "itemname": return MSBuildValueKind.ItemName;
			case "version": return MSBuildValueKind.Version;
			case "folder": return MSBuildValueKind.Folder;
			case "folderlist": return MSBuildValueKind.FolderList;
			case "runtimeidlist": return MSBuildValueKind.RuntimeIDList;
			case "runtimeid": return MSBuildValueKind.RuntimeID;
			case "targetframework": return MSBuildValueKind.TargetFramework;
			case "targetframeworklist": return MSBuildValueKind.TargetFrameworkList;
			case "url": return MSBuildValueKind.Url;
			case "suffixedversion": return MSBuildValueKind.SuffixedVersion;
			default:
				//accept unknown values in case we run into newer schema formats
				LoggingService.LogDebug ($"Unknown value kind '{valueKind}'");
				return null;
			}
		}

		MetadataInfo GetMetadataReference (string desc, Dictionary<string, MetadataInfo> parent, string parentName)
		{
			if (!desc.StartsWith ("%(", StringComparison.Ordinal) || !desc.EndsWith (")", StringComparison.Ordinal)) {
				return null;
			}
			var split = desc.Substring (2, desc.Length - 3).Split ('.');
			if (split.Length == 1 && parent != null && parent.TryGetValue (split[0], out MetadataInfo sibling)) {
				return sibling;
			}
			if (split.Length == 2 && Items.TryGetValue (split[0], out ItemInfo item) && item.Metadata.TryGetValue (split [1], out MetadataInfo cousin)) {
				return cousin;
			}
			throw new Exception ($"Invalid metadata reference {desc} in item {parentName}");
		}

		MetadataInfo WithNewName (MetadataInfo meta, string name)
		{
			return new MetadataInfo (
				name, meta.Description, meta.WellKnown, meta.Required,
				meta.ValueKind, meta.Values, meta.DefaultValue, meta.ValueSeparators);
		}

		Dictionary<string, MetadataInfo> GetMetadata (string itemName, JObject metaObj)
		{
			var metadata = new Dictionary<string, MetadataInfo> ();
			foreach (var kv in metaObj) {
				var name = kv.Key;

				//simple version, just a description string
				if (kv.Value is JValue value) {
					var desc = ((string)value.Value).Trim ();
					var reference = GetMetadataReference (desc, metadata, itemName);
					if (reference != null) {
						metadata [name] = WithNewName (reference, name);
					} else {
						metadata [name] = new MetadataInfo (name, desc, false);
					}
					continue;
				}

				string description = null, valueSeparators = null, defaultValue = null;
				bool required = false;
				MSBuildValueKind kind = MSBuildValueKind.MetadataExpression;
				List<ValueInfo> values = null;
				foreach (var mkv in (JObject)kv.Value) {
					switch (mkv.Key) {
					case "description":
						description = (string)((JValue)mkv.Value).Value;
						break;
					case "kind":
						kind = ParseValueKind ((string)((JValue)mkv.Value).Value) ?? kind;
						break;
					case "values":
						switch (mkv.Value) {
						case JValue jv:
							var metaRef = GetMetadataReference ((string)jv.Value, metadata, itemName);
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
					case "defaultValue":
						defaultValue = (string)((JValue)mkv.Value).Value;
						break;
					case "required":
						required = (bool)((JValue)mkv.Value).Value;
						break;
					default:
						throw new Exception ($"Unknown property {mkv.Key} in metadata {kv.Key}");
					}
				}
				metadata[name] = new MetadataInfo (name, description, false, required, kind, values, defaultValue, valueSeparators?.ToCharArray ());
			}
			return metadata;
		}

		List<ValueInfo> GetValues (JObject value)
		{
			var values = new List<ValueInfo> ();
			foreach (var ikv in value) {
				values.Add (new ValueInfo (ikv.Key, (string)((JValue)ikv.Value).Value));
			}
			return values;
		}

		List<ValueInfo> GetValues (JArray arr)
		{
			var values = new List<ValueInfo> ();
			foreach (var val in arr) {
				values.Add (new ValueInfo ((string)((JValue)val).Value, null));
			}
			return values;
		}

		public bool IsPrivate (string name)
		{
			//assembly everything in a schema is public
			return false;
		}
	}
}