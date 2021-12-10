// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

using MonoDevelop.MSBuild.Language.Typesystem;
using MonoDevelop.Xml.Parser;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MonoDevelop.MSBuild.Schema;

partial class MSBuildSchema
{
	class SchemaLoadState
	{
		readonly string origin;

		public SchemaLoadState (string origin)
		{
			this.origin = origin;
		}

		public Dictionary<string, CustomTypeInfo> CustomTypes { get; private set; }
		public List<MSBuildSchemaLoadError> Errors { get; private set; }

		void AddError (JToken position, string message, DiagnosticSeverity severity)
			=> (Errors ??= new List<MSBuildSchemaLoadError> ()).Add (
				new MSBuildSchemaLoadError (
						message,
						severity,
						origin,
						position is IJsonLineInfo lineInfo ? (lineInfo.LinePosition, lineInfo.LinePosition) : null,
						position.Path
					)
				);

		public void AddError (JToken position, string error) => AddError (position, error, DiagnosticSeverity.Error);
		public void AddWarning (JToken position, string error) => AddError (position, error, DiagnosticSeverity.Warning);

		public void LoadCustomTypes (JObject value)
		{
			var dict = new Dictionary<string, CustomTypeInfo> ();
			foreach (var kv in value) {
				if (ReadCustomTypeDefinition (kv.Value) is CustomTypeInfo definition) {
					dict.Add (kv.Key, definition);
				}
			}
			CustomTypes = dict;
		}

		public IEnumerable<(string name, PropertyInfo property)> ReadProperties (JObject properties)
		{
			foreach (var kv in properties) {
				var name = kv.Key;
				if (GetErrorIfInvalidMSBuildIdentifier (name) is string error) {
					AddError (kv.Value ?? properties, error);
					continue;
				}

				if (kv.Value is JValue simpleVal) {
					yield return (name, new PropertyInfo (name, (string)simpleVal.Value));
					continue;
				}

				var propObj = (JObject)kv.Value;

				string description = null, defaultValue = null, deprecationMessage = null;
				bool isDeprecated = false;

				var typeLoader = new TypeInfoReader (this, propObj, false);

				foreach (var pkv in propObj) {
					switch (pkv.Key) {
					case "description":
						description = pkv.Value.Value<string> ();
						break;
					case "defaultValue":
						defaultValue = (string)((JValue)pkv.Value).Value;
						break;
					case "deprecationMessage":
						isDeprecated = true;
						deprecationMessage = (string)((JValue)pkv.Value).Value;
						break;
					default:
						if (typeLoader.TryHandle (pkv.Key, pkv.Value)) {
							break;
						}
						AddWarning (pkv.Value ?? properties, $"Unknown property");
						break;
					}
				}

				(MSBuildValueKind kind, CustomTypeInfo customType) = typeLoader.TryMaterialize ();

				yield return (name, new PropertyInfo (name, description, false, kind, customType, defaultValue, isDeprecated, deprecationMessage));
			}
		}

		public IEnumerable<(string name, ItemInfo item)> ReadItems (JObject items)
		{
			foreach (var kv in items) {
				var name = kv.Key;
				if (GetErrorIfInvalidMSBuildIdentifier (name) is string error) {
					AddError (kv.Value ?? items, error);
					continue;
				}

				string description = null, includeDescription = null, deprecationMessage = null;
				JObject metadata = null;
				bool isDeprecated = false;

				var typeLoader = new TypeInfoReader (this, items, true);

				foreach (var ikv in (JObject)kv.Value) {
					switch (ikv.Key) {
					case "description":
						description = (string)((JValue)ikv.Value).Value;
						break;
					case "includeDescription":
						includeDescription = (string)((JValue)ikv.Value).Value;
						break;
					case "metadata":
						metadata = (JObject)ikv.Value;
						break;
					case "deprecationMessage":
						isDeprecated = true;
						deprecationMessage = (string)((JValue)ikv.Value).Value;
						break;
					default:
						if (typeLoader.TryHandle (ikv.Key, ikv.Value)) {
							break;
						}
						AddWarning (ikv.Value ?? items, $"Unknown property");
						break;
					}
				}

				(MSBuildValueKind kind, CustomTypeInfo customType) = typeLoader.TryMaterialize ();

				// FIXME: why this restriction?
				// NOTE: even when the kind is not custom, the customType object might be a nuget package type, which is valid
				if (kind.IsCustomType ()) {
					AddError (kv.Value ?? items, $"Item '{name}' has custom type value, which is not permitted for items");
					kind = MSBuildValueKind.Unknown;
					customType = null;
				}

				var item = new ItemInfo (name, description, includeDescription, kind, customType, null, isDeprecated, deprecationMessage);

				if (metadata != null) {
					AddMetadata (item, metadata);
				}

				yield return (name, item);
			}
		}

		void AddMetadata (ItemInfo item, JObject metaObj)
		{
			foreach (var kv in metaObj) {
				var name = kv.Key;
				if (GetErrorIfInvalidMSBuildIdentifier (name) is string error) {
					AddError (kv.Value ?? metaObj, error);
					continue;
				}
				var val = ReadMetadata (name, kv.Value);
				val.Item = item;
				item.Metadata.Add (name, val);
			}
		}

		MetadataInfo ReadMetadata (string name, JToken value)
		{
			//simple version, just a description string
			if (value is JValue v) {
				var desc = ((string)v.Value).Trim ();
				return new MetadataInfo (name, desc);
			}

			var metaObj = (JObject)value;

			string description = null, defaultValue = null, deprecationMessage = null;
			bool required = false;
			bool isDeprecated = false;

			var typeLoader = new TypeInfoReader (this, metaObj, false);

			foreach (var mkv in metaObj) {
				switch (mkv.Key) {
				case "description":
					description = (string)((JValue)mkv.Value).Value;
					break;
				case "defaultValue":
					defaultValue = (string)((JValue)mkv.Value).Value;
					break;
				case "isRequired":
					required = (bool)((JValue)mkv.Value).Value;
					break;
				case "deprecationMessage":
					isDeprecated = true;
					deprecationMessage = (string)((JValue)mkv.Value).Value;
					break;
				default:
					if (typeLoader.TryHandle (mkv.Key, mkv.Value)) {
						break;
					}
					AddWarning (mkv.Value ?? value, $"Unknown property");
					break;
				}
			}

			(MSBuildValueKind kind, CustomTypeInfo customType) = typeLoader.TryMaterialize ();

			return new MetadataInfo (
				name, description, false, required, kind, null,
				customType, defaultValue, isDeprecated, deprecationMessage
			);
		}

		public (MetadataGroup metadata, string[] appliesTo) ReadMetadataGroup (JObject obj)
		{
			string[] appliesTo = null;
			var metadata = new List<MetadataInfo> ();

			foreach (var kv in obj) {
				if (kv.Key == "$appliesTo") {
					if (kv.Value is JArray arr) {
						bool hasErrors = false;
						appliesTo = new string[arr.Count];
						for (int i = 0; i < arr.Count; i++) {
							var appliesToName = (string)((JValue)arr[i]).Value;
							if (GetErrorIfInvalidMSBuildIdentifier (appliesToName) is string error) {
								AddError (arr, error);
								continue;
							}
							appliesTo[i] = appliesToName;
						}
						if (hasErrors) {
							appliesTo = appliesTo.Where (v => v != null).ToArray ();
						}
					} else {
						appliesTo = new[] { (string)kv.Value };
					}
					continue;
				}
				metadata.Add (ReadMetadata (kv.Key, kv.Value));
			}

			if (appliesTo == null) {
				AddError (obj, "Metadata groups must have $appliesTo keys");
				appliesTo = Array.Empty<string> ();
			}

			return (new MetadataGroup (metadata), appliesTo);
		}

		public struct MetadataGroup
		{
			readonly List<MetadataInfo> metadata;
			bool needsCopy;

			public MetadataGroup (List<MetadataInfo> metadata)
			{
				this.metadata = metadata;
				needsCopy = false;
			}

			public void ApplyToItem (ItemInfo item)
			{
				foreach (var m in metadata) {
					//the original metadata object gets parented on the first item, subsequent items get a copy
					var toAdd = needsCopy ? new MetadataInfo (m.Name, m.Description, m.Reserved, m.Required, m.ValueKind, item, m.CustomType, m.DefaultValue) : m;
					item.Metadata.Add (toAdd.Name, toAdd);
					toAdd.Item = item;
				}
				needsCopy = true;
			}
		}

		public (MSBuildValueKind kind, CustomTypeInfo customType) ReadType (JToken token)
		{
			if (token is JValue typeVal) {
				string kindStr = (string)typeVal.Value;
				if (TryParseValueKind (kindStr) is MSBuildValueKind parsedKind) {
					return (parsedKind, null);
				}
				AddWarning (token, $"Unknown value kind '{kindStr}'");
				return (MSBuildValueKind.Unknown, null);
			}

			if (token is JArray conciseDef) {
				return (MSBuildValueKind.CustomType, ReadMinimalCustomTypeDefinition (conciseDef));
			}

			(var definition, var reference) = ReadCustomTypeDefinitionOrReference ((JObject)token);

			if (reference != null) {
				if (CustomTypes.TryGetValue (reference, out var resolved)) {
					return (MSBuildValueKind.CustomType, resolved);
				}
				AddWarning (token, $"Could not resolve type reference '{reference}'");
			}

			if (definition != null) {
				return (MSBuildValueKind.CustomType, definition);
			}

			// ReadCustomTypeDefinitionOrReference should hav reported any errors
			return (MSBuildValueKind.Unknown, null);
		}

		CustomTypeInfo ReadCustomTypeDefinition (JToken token)
		{
			if (token is JArray conciseDef) {
				return ReadMinimalCustomTypeDefinition (conciseDef);
			}

			(var definition, var reference) = ReadCustomTypeDefinitionOrReference ((JObject)token);
			if (reference != null) {
				AddWarning (token, $"Type definition cannot be a type reference");
				return null;
			}

			return definition;
		}

		static CustomTypeInfo ReadMinimalCustomTypeDefinition (JArray value) => new (
			value
			.Select (v => new CustomTypeValue (v.Value<string> (), null))
			.ToArray ()
		);

		(CustomTypeInfo definition, string reference) ReadCustomTypeDefinitionOrReference (JObject obj)
		{
			var enumerator = obj.GetEnumerator ();
			if (!enumerator.MoveNext ()) {
				AddWarning (obj, $"Empty custom type");
				return (null, null);
			}

			var values = new List<CustomTypeValue> ();

			string name = null;
			string description = null;
			bool allowUnknownValues = false;

			bool foundAnyNonRef = false;

			do {
				var ikv = enumerator.Current;
				switch (ikv.Key) {
				case "$ref":
					if (foundAnyNonRef) {
						AddWarning (obj, "When '$ref' is present it should be the only property");
					}
					var target = (string)((JValue)ikv.Value).Value;
					const string refPrefix = "#/types/";
					if (target.StartsWith (refPrefix, StringComparison.OrdinalIgnoreCase)) {
						target = target.Substring (refPrefix.Length);
						return (null, target);
					}
					AddWarning (obj, $"Only references of the form '{refPrefix}name' are supported");
					return (null, null);
				case "name":
					name = (string)((JValue)ikv.Value).Value;
					if (GetErrorIfInvalidCustomTypeDisplayName (name) is string error) {
						AddWarning (obj, error);
						name = null;
					}
					break;
				case "description":
					description = (string)((JValue)ikv.Value).Value;
					break;
				case "allowUnknownValues":
					allowUnknownValues = (bool)((JValue)ikv.Value).Value;
					break;
				case "values":
					foreach (var val in (JObject)ikv.Value) {
						values.Add (new CustomTypeValue (val.Key, (string)((JValue)val.Value).Value));
					}
					break;
				default:
					AddWarning (obj, $"Unknown key '{ikv.Key}'");
					break;
				}
				foundAnyNonRef = true;
			} while (enumerator.MoveNext ());

			if (values.Count == 0) {
				AddWarning (obj, $"Empty custom type");
				return (null, null);
			}

			return (new CustomTypeInfo (values, name, description, allowUnknownValues), null);
		}

		public static string GetErrorIfInvalidCustomTypeDisplayName (string name)
		{
			// matches schema logic: ^([a-z][a-z\\d-]*)$
			if (name.Length == 0) {
				return "Name is empty";
			}

			if (name[0] switch {
				>= 'A' and <= 'Z' => true,
				>= 'a' and <= 'z' => true,
				_ => false
			} == false) {
				return "Custom type name must start with a lowercase letter";
			}

			for (int i = 1; i < name.Length; i++) {
				if (name[i] switch {
					>= 'A' and <= 'Z' => true,
					>= 'a' and <= 'z' => true,
					>= '0' and <= '9' => true,
					'-' => true,
					_ => false
				} == false) {
					return "Custom type name may only contain lowercase letters, numbers and dashes";
				}
			}

			return null;
		}

		public static string GetErrorIfInvalidMSBuildIdentifier (string identifier)
		{
			// matches schema logic: ^([A-Za-z_][A-Za-z\\d_-]*)$
			if (identifier.Length == 0) {
				return "Identifier is empty";
			}

			if (identifier[0] switch {
				>= 'A' and <= 'Z' => true,
				>= 'a' and <= 'z' => true,
				'_' => true,
				_ => false
			} == false) {
				return "MSBuild identifier must start with letter or underscore";
			}

			for (int i = 1; i < identifier.Length; i++) {
				if (identifier[i] switch {
					>= 'A' and <= 'Z' => true,
					>= 'a' and <= 'z' => true,
					>= '0' and <= '9' => true,
					'_' => true,
					'-' => true,
					_ => false
				} == false) {
					return "MSBuild identifier may only contain letter, digit, underscore and dash characters";
				}
			}

			return null;
		}
	}
}
