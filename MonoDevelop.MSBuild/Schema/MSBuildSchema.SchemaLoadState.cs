// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

using MonoDevelop.MSBuild.Language.Typesystem;
using MonoDevelop.Xml.Analysis;

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

		public Dictionary<string, CustomTypeInfo>? CustomTypes { get; private set; }
		public List<MSBuildSchemaLoadError>? Errors { get; private set; }

		void AddError (JToken position, string message, XmlDiagnosticSeverity severity)
			=> (Errors ??= new List<MSBuildSchemaLoadError> ()).Add (
				new MSBuildSchemaLoadError (
						message,
						severity,
						origin,
						position is IJsonLineInfo lineInfo ? (lineInfo.LineNumber, lineInfo.LinePosition) : null,
						position.Path
					)
				);

		public void AddError (JToken position, string error) => AddError (position, error, XmlDiagnosticSeverity.Error);
		public void AddWarning (JToken position, string error) => AddError (position, error, XmlDiagnosticSeverity.Warning);

		[MemberNotNull(nameof (CustomTypes))]
		public void LoadCustomTypes (JObject customTypeCollection)
		{
			var dict = new Dictionary<string, CustomTypeInfo> ();
			foreach ((string customTypeId, JToken? customTypeDef) in customTypeCollection) {
				if (ReadCustomTypeDefinition (customTypeCollection, customTypeId, customTypeDef) is CustomTypeInfo definition) {
					dict.Add (customTypeId, definition);
				}
			}
			CustomTypes = dict;
		}

		public IEnumerable<PropertyInfo> ReadProperties (JObject propertyCollection)
		{
			foreach ((string propertyName, JToken? propertyDef) in propertyCollection) {
				if (GetErrorIfInvalidMSBuildIdentifier (propertyName) is string error) {
					AddError (propertyDef ?? propertyCollection, error);
					continue;
				}

				if (propertyDef is JValue simpleVal && simpleVal.Value is string simpleDesc) {
					yield return new PropertyInfo (propertyName, simpleDesc);
					continue;
				}

				if (propertyDef is not JObject propertyDefObj) {
					AddError (propertyDef ?? propertyCollection, $"Property '{propertyName}' definition must be an object or description string");
					continue;
				}

				string? description = null, defaultValue = null, deprecationMessage = null, helpUrl = null;

				var typeLoader = new TypeInfoReader (this, propertyDefObj, false);

				foreach ((string defPropName, JToken? defPropVal) in propertyDefObj) {
					if (defPropVal is null) {
						AddError (defPropVal ?? propertyDefObj, $"Property '{propertyName}' definition property '{defPropName}' has no value");
						continue;
					}
					bool GetValueString ([NotNullWhen (true)] out string? value)
					{
						if (defPropVal is JValue v && (value = v.Value as string) is not null) {
							return true;
						}
						AddError (defPropVal ?? propertyDefObj, $"Property '{propertyName}' definition property '{defPropName}' must be a string");
						value = null;
						return false;
					}

					switch (defPropName) {
					case "description":
						GetValueString (out description);
						break;
					case "defaultValue":
						GetValueString (out defaultValue);
						break;
					case "deprecationMessage":
						GetValueString (out deprecationMessage);
						break;
					case "helpUrl":
						GetValueString (out helpUrl);
						break;
					default:
						if (typeLoader.TryHandle (defPropName, defPropVal)) {
							break;
						}
						AddWarning (defPropVal ?? propertyDefObj, $"Property '{propertyName}' definition has unknown property '{defPropName}'");
						break;
					}
				}

				(MSBuildValueKind kind, CustomTypeInfo customType) = typeLoader.TryMaterialize ();

				yield return new PropertyInfo (propertyName, description, kind, customType, defaultValue, deprecationMessage, helpUrl);
			}
		}

		public IEnumerable<ItemInfo> ReadItems (JObject itemCollection)
		{
			foreach ((string itemName, JToken? itemDef) in itemCollection) {
				if (GetErrorIfInvalidMSBuildIdentifier (itemName) is string error) {
					AddError (itemDef ?? itemCollection, error);
					continue;
				}

				string? description = null, includeDescription = null, deprecationMessage = null, helpUrl = null;
				JObject? metadata = null;

				var typeLoader = new TypeInfoReader (this, itemCollection, true);

				if (itemDef is JValue simpleVal && simpleVal.Value is string simpleDesc) {
					yield return new ItemInfo (itemName, simpleDesc);
					continue;
				}

				if (itemDef is not JObject itemDefObj) {
					AddError (itemDef ?? itemCollection, $"Item '{itemName}' value must be an object or description string");
					continue;
				}

				foreach ((string defPropName, JToken? defPropVal) in itemDefObj) {
					if (defPropVal is null) {
						AddError (defPropVal ?? itemDefObj, $"Item '{itemName}' definition property '{defPropName}' has no value");
						continue;
					}

					bool GetValueString ([NotNullWhen (true)] out string? value)
					{
						if (defPropVal is JValue v && (value = v.Value as string) is not null) {
							return true;
						}
						AddError (defPropVal ?? itemDefObj, $"Item '{itemName}' definition property '{defPropName}' must be a string");
						value = null;
						return false;
					}

					switch (defPropName) {
					case "description":
						GetValueString (out description);
						break;
					case "includeDescription":
						GetValueString (out includeDescription);
						break;
					case "metadata":
						if ((metadata = defPropVal as JObject) is null) {
							AddError (defPropVal ?? itemDef, $"Item '{itemName}' property '{defPropName}' must be an object");
						}
						break;
					case "deprecationMessage":
						GetValueString (out deprecationMessage);
						break;
					case "helpUrl":
						GetValueString (out helpUrl);
						break;
					default:
						if (typeLoader.TryHandle (defPropName, defPropVal)) {
							break;
						}
						AddWarning (defPropVal ?? itemDefObj, $"Item '{itemName}' definition has unknown property '{defPropName}'");
						break;
					}
				}

				// NOTE: even when the kind is not custom, the customType object might be a NuGet package type, which is valid
				(MSBuildValueKind kind, CustomTypeInfo? customType) = typeLoader.TryMaterialize ();

				var item = new ItemInfo (itemName, description, includeDescription, kind, customType, null, deprecationMessage, helpUrl);

				if (metadata != null) {
					AddItemMetadata (item, metadata);
				}

				yield return item;
			}
		}

		void AddItemMetadata (ItemInfo item, JObject metadataCollection)
		{
			foreach ((string metadataName, JToken? metadataDef) in metadataCollection) {
				if (GetErrorIfInvalidMSBuildIdentifier (metadataName) is string error) {
					AddError (metadataDef ?? metadataCollection, error);
					continue;
				}
				if (ReadMetadata (item.Name, metadataName, metadataDef, metadataCollection) is MetadataInfo metadataInfo) {
					metadataInfo.Item = item;
					item.Metadata.Add (metadataName, metadataInfo);
				}
			}
		}

		MetadataInfo? ReadMetadata (string? itemName, string metadataName, JToken? metadataDef, JObject metadataContainer)
		{
			string FormatName() => itemName is null ? metadataName : $"{itemName}.{metadataName}";

			//simple version, just a description string
			if (metadataDef is JValue simpleVal && simpleVal.Value is string simpleDesc) {
				return new MetadataInfo (metadataName, simpleDesc);
			}

			if (metadataDef is not JObject metadataDefObj) {
				AddError (metadataDef ?? metadataContainer, $"Item metadata '{FormatName()}' definition must be an object or description string");
				return null;
			}

			string? description = null, defaultValue = null, deprecationMessage = null, helpUrl = null;
			bool? required = null;

			var typeLoader = new TypeInfoReader (this, metadataDefObj, false);

			foreach ((string defPropName, JToken? defPropVal) in metadataDefObj) {
				if (defPropVal is null) {
					AddError (defPropVal ?? metadataDefObj, $"Item metadata '{FormatName ()}' definition property '{defPropName}' has no value");
					continue;
				}

				bool GetValueString ([NotNullWhen (true)] out string? value)
				{
					if (defPropVal is JValue v && (value = v.Value as string) is not null) {
						return true;
					}
					AddError (defPropVal ?? metadataDefObj, $"Item metadata '{FormatName()}' definition property '{defPropName}' must be a string");
					value = null;
					return false;
				}

				bool GetValueBool ([NotNullWhen (true)] out bool? value)
				{
					if (defPropVal is JValue v && (value = v.Value as bool?) is not null) {
						return true;
					}
					AddError (defPropVal ?? metadataDefObj, $"Item metadata '{FormatName ()}' definition property '{defPropName}' must be a bool");
					value = null;
					return false;
				}

				switch (defPropName) {
				case "description":
					GetValueString (out description);
					break;
				case "defaultValue":
					GetValueString (out defaultValue);
					break;
				case "isRequired":
					GetValueBool (out required);
					break;
				case "deprecationMessage":
					GetValueString (out deprecationMessage);
					break;
				case "helpUrl":
					GetValueString (out helpUrl);
					break;
				default:
					if (typeLoader.TryHandle (defPropName, defPropVal)) {
						break;
					}
					AddWarning (defPropVal ?? metadataDefObj, $"Item metadata '{FormatName()}' definition has unknown property '{defPropName}'");
					break;
				}
			}

			(MSBuildValueKind kind, CustomTypeInfo customType) = typeLoader.TryMaterialize ();

			return new MetadataInfo (
				metadataName, description, false, required ?? false, kind, null,
				customType, defaultValue, deprecationMessage, helpUrl
			);
		}

		public (MetadataGroup metadata, string[] appliesTo) ReadMetadataGroup (JObject metadataGroup)
		{
			string[]? appliesTo = null;
			var metadata = new List<MetadataInfo> ();

			foreach ((string groupPropName, JToken? groupPropVal) in metadataGroup) {
				if (groupPropName == "$appliesTo") {
					if (groupPropVal is JArray conciseMetadataDef) {
						bool hasErrors = false;
						appliesTo = new string[conciseMetadataDef.Count];
						for (int i = 0; i < conciseMetadataDef.Count; i++) {
							if (conciseMetadataDef[i] is not JValue jv || jv.Value is not string appliesToName) {
								AddError (conciseMetadataDef[i] ?? groupPropVal ?? metadataGroup, "$appliesTo array values must be strings");
								continue;
							}
							if (GetErrorIfInvalidMSBuildIdentifier (appliesToName) is string error) {
								AddError (conciseMetadataDef, error);
								continue;
							}
							appliesTo[i] = appliesToName;
						}
						if (hasErrors) {
							appliesTo = appliesTo.Where (v => v != null).ToArray ();
						}
					} else if (groupPropVal is JValue v && v.Value is string simpleValue) {
						if (GetErrorIfInvalidMSBuildIdentifier (simpleValue) is string error) {
							AddError (groupPropVal, error);
							continue;
						}
						appliesTo = new[] { simpleValue };
					} else {
						AddError (groupPropVal ?? metadataGroup, "$appliesTo must be a string or array of strings");
					}
					continue;
				}

				if (ReadMetadata (null, groupPropName, groupPropVal, metadataGroup) is MetadataInfo metadataInfo) {
					metadata.Add (metadataInfo);
				}
			}

			if (appliesTo == null) {
				AddError (metadataGroup, "Metadata groups must have a $appliesTo key");
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

		public IEnumerable<TargetInfo> ReadTargets (JObject targetCollection)
		{
			foreach ((string targetName, JToken? targetDef) in targetCollection) {
				if (GetErrorIfInvalidMSBuildIdentifier (targetName) is string error) {
					AddError (targetDef ?? targetCollection, error);
					continue;
				}

				string? description = null, deprecationMessage = null, helpUrl = null;

				if (targetDef is JValue simpleVal && simpleVal.Value is string simpleDesc) {
					yield return new TargetInfo (targetName, simpleDesc);
					continue;
				}

				if (targetDef is not JObject targetDefObj) {
					AddError (targetDef ?? targetCollection, $"Target '{targetName}' value must be an object or description string");
					continue;
				}

				foreach ((string defPropName, JToken? defPropVal) in targetDefObj) {

					bool GetValueString ([NotNullWhen (true)] out string? value)
					{
						if (defPropVal is JValue v && (value = v.Value as string) is not null) {
							return true;
						}
						AddError (defPropVal ?? targetDefObj, $"Target '{targetName}' definition property '{defPropName}' must be a string");
						value = null;
						return false;
					}

					switch (defPropName) {
					case "description":
						GetValueString (out description);
						break;
					case "deprecationMessage":
						GetValueString (out deprecationMessage);
						break;
					case "helpUrl":
						GetValueString (out helpUrl);
						break;
					default:
						AddWarning (defPropVal ?? targetDefObj, $"Target '{targetName}' definition has unknown property '{defPropName}'");
						break;
					}
				}

				yield return new TargetInfo (targetName, description, deprecationMessage, helpUrl);
			}
		}

		public (MSBuildValueKind kind, CustomTypeInfo? customType) ReadType (JToken token)
		{
			if (token is JValue typeVal && typeVal.Value is string kindStr) {
				if (TryParseValueKind (kindStr) is MSBuildValueKind parsedKind) {
					return (parsedKind, null);
				}
				AddWarning (token, $"Unknown intrinsic type '{kindStr}'");
				return (MSBuildValueKind.Unknown, null);
			}

			if (token is JArray conciseDef) {
				return (MSBuildValueKind.CustomType, ReadConciseCustomTypeDefinition (conciseDef));
			}

			if (token is not JObject typeDefObj) {
				AddError (token, "Type must be an intrinsic type string, type definition array, type definition object, or type reference object");
				return (MSBuildValueKind.Unknown, null);
			}

			(var definition, var reference) = ReadCustomTypeDefinitionOrReference (typeDefObj);

			if (reference != null) {
				if (CustomTypes is not null && CustomTypes.TryGetValue (reference, out var resolved)) {
					return (MSBuildValueKind.CustomType, resolved);
				}
				AddError (token, $"Could not resolve type reference '{reference}'");
			}

			if (definition != null) {
				return (MSBuildValueKind.CustomType, definition);
			}

			// ReadCustomTypeDefinitionOrReference should hav reported any errors
			return (MSBuildValueKind.Unknown, null);
		}

		CustomTypeInfo? ReadCustomTypeDefinition (JObject customTypeContainer, string? customTypeId, JToken? customTypeDef)
		{
			if (customTypeDef is JArray conciseDef) {
				return ReadConciseCustomTypeDefinition (conciseDef);
			}
			if (customTypeDef is not JObject customTypeDefObj) {
				AddError (customTypeDef ?? customTypeContainer, customTypeId is not null
					? $"Custom type definition '{customTypeId}' must be an object or array"
					: $"Custom type definition must be an object or array");
				return null;
			}

			(var definition, var reference) = ReadCustomTypeDefinitionOrReference (customTypeDefObj);
			if (reference != null) {
				AddWarning (customTypeDefObj, customTypeId is not null
					? $"Custom type definition '{customTypeId}' cannot be a type reference"
					: $"Custom type definition cannot be a type reference");
				return null;
			}

			return definition;
		}

		CustomTypeInfo ReadConciseCustomTypeDefinition (JArray conciseCustomTypeDef)
		{
			var values = new List<CustomTypeValue> (conciseCustomTypeDef.Count);
			foreach (var defVal in conciseCustomTypeDef) {
				if (defVal is not JValue jv || jv.Value is not string customTypeValue) {
					AddError (defVal ?? conciseCustomTypeDef, "Concise custom type definition values must be strings");
					continue;
				}
				values.Add (new CustomTypeValue (customTypeValue, null));
			}
			return new (values);
		}

		(CustomTypeInfo? definition, string? reference) ReadCustomTypeDefinitionOrReference (JObject customTypeObj)
		{
			var enumerator = customTypeObj.GetEnumerator ();
			if (!enumerator.MoveNext ()) {
				AddWarning (customTypeObj, $"Empty custom type definition");
				return (null, null);
			}

			var values = new List<CustomTypeValue> ();

			string? name = null;
			string? description = null;
			bool? allowUnknownValues = null;
			MSBuildValueKind? baseValueKind = null;
			bool? caseSensitive = null;
			string? helpUrl = null;

			bool foundAnyNonRef = false;

			ImmutableDictionary<string, object> analyzerHints = ImmutableDictionary<string, object>.Empty;

			do {
				(string defPropName, JToken? defPropVal) = enumerator.Current;

				bool GetValueString ([NotNullWhen (true)] out string? value)
				{
					if (defPropVal is JValue v && (value = v.Value as string) is not null) {
						return true;
					}
					AddError (defPropVal ?? customTypeObj, $"Custom type definition property '{defPropName}' must be a string");
					value = null;
					return false;
				}

				bool GetValueBool ([NotNullWhen (true)] out bool? value)
				{
					if (defPropVal is JValue v && (value = v.Value as bool?) is not null) {
						return true;
					}
					AddError (defPropVal ?? customTypeObj, $"Custom type definition property '{defPropName}' must be a bool");
					value = null;
					return false;
				}

				switch (defPropName) {
				case "$ref":
					if (foundAnyNonRef || enumerator.MoveNext ()) {
						AddWarning (customTypeObj, "Custom type references must have only the property '$ref'");
					}
					const string refPrefix = "#/types/";
					if (GetValueString (out var target) && target.StartsWith (refPrefix, StringComparison.OrdinalIgnoreCase)) {
						target = target.Substring (refPrefix.Length);
						return (null, target);
					}
					AddWarning (customTypeObj, $"Custom type references must have the the form '{refPrefix}name'");
					return (null, null);
				case "name":
					if (GetValueString (out name) && GetErrorIfInvalidCustomTypeDisplayName (name) is string error) {
						AddWarning (customTypeObj, error);
						name = null;
					}
					break;
				case "description":
					GetValueString (out description);
					break;
				case "allowUnknownValues":
					GetValueBool(out allowUnknownValues);
					break;
				case "caseSensitive":
					GetValueBool (out caseSensitive);
					break;
				case "baseType":
					if (defPropVal is JValue baseKindVal && baseKindVal.Value is string kindStr && TryParseValueKind (kindStr) is MSBuildValueKind parsedKind && PermittedBaseKinds.Contains (parsedKind)) {
						baseValueKind = parsedKind;
					} else {
						var kindNames = GetValueKindNames().ToDictionary (k => k.kind, k => k.name);
						var permittedKindNames = PermittedBaseKinds.Select (name => kindNames[name]);
						AddWarning (defPropVal ?? customTypeObj, $"Custom type definition property '{defPropName}' must have one of the following values: '{string.Join("', '", permittedKindNames)}'");
					}
					break;
				case "values":
					if (defPropVal is not JObject valuesObj) {
						AddError (defPropVal ?? customTypeObj, $"Custom type definition property 'values' must be a non-empty object");
						return (null, null);
					}
					foreach ((string valueName, JToken? valueDescToken) in valuesObj) {
						values.Add (ReadCustomTypeValue (valuesObj, valueName, valueDescToken));
					}
					break;
				case "analyzerHints": {
					if (defPropVal is not JObject analyzerHintsObj) {
						AddError (defPropVal ?? customTypeObj, $"Custom type definition property 'analyzerHints' must be a non-empty object");
						return (null, null);
					}
					foreach ((string hintName, JToken? hintValueToken) in analyzerHintsObj) {
						if (string.IsNullOrEmpty (hintName)) {
							AddError (hintValueToken ?? analyzerHintsObj, $"Analyzer hint must have non-empty name");
						}
						if (hintValueToken is JValue hintValue && hintValue.Value is not null) {
							analyzerHints = analyzerHints.Add (hintName, hintValue.Value);
						} else {
							AddError (hintValueToken ?? analyzerHintsObj, $"Analyzer hint '{hintName}' must have a simple non-null value");
						}
					}
					break;
				}
				case "helpUrl":
					GetValueString (out helpUrl);
					break;
				default:
					AddWarning (defPropVal ?? customTypeObj, $"Custom type definition has unknown property '{defPropName}'");
					break;
				}
				foundAnyNonRef = true;
			} while (enumerator.MoveNext ());

			if (values.Count == 0) {
				allowUnknownValues = true;
			}

			return (new CustomTypeInfo (values, name, description, allowUnknownValues ?? false, baseValueKind ?? MSBuildValueKind.Unknown, caseSensitive ?? false, analyzerHints, helpUrl), null);
		}

		CustomTypeValue ReadCustomTypeValue (JObject customTypeValueCollection, string customTypeValueName, JToken? customTypeValueToken)
		{
			if (customTypeValueToken is JValue simpleVal && simpleVal.Value is string simpleDesc) {
				return new CustomTypeValue (customTypeValueName, simpleDesc);
			}

			if (customTypeValueToken is not JObject customTypeValueObj) {
				AddError (customTypeValueToken ?? customTypeValueCollection, $"Custom type value '{customTypeValueName}' must be an object or description string");
				return new CustomTypeValue (customTypeValueName, null);
			}

			string? description = null, deprecationMessage = null, helpUrl = null;
			string[]? aliases = null;

			foreach ((string defPropName, JToken? defPropVal) in customTypeValueObj) {

				bool GetValueString ([NotNullWhen (true)] out string? value)
				{
					if (defPropVal is JValue v && (value = v.Value as string) is not null) {
						return true;
					}
					AddError (defPropVal ?? customTypeValueObj, $"Custom type value '{customTypeValueName}' definition property '{defPropName}' must be a string");
					value = null;
					return false;
				}

				bool GetValueStringArray ([NotNullWhen (true)] out string[]? value)
				{
					if (defPropVal is JArray arr && arr.Count > 0) {
						value = new string [arr.Count];
						for (int i = 0; i < arr.Count; i++) {
							if (arr[i] is JValue v && v.Value is string s && !string.IsNullOrEmpty (s)) {
								value[i] = s;
							} else {
								AddError (arr[i] ?? arr, $"Custom type value '{customTypeValueName}' definition property '{defPropName}' must be an array of non-empty strings");
								value = null;
								return false;
							}
						}
						return true;
					}
					AddError (defPropVal ?? customTypeValueObj, $"Custom type value '{customTypeValueName}' definition property '{defPropName}' must be an array of non-empty strings");
					value = null;
					return false;
				}

				switch (defPropName) {
				case "description":
					GetValueString (out description);
					break;
				case "deprecationMessage":
					GetValueString (out deprecationMessage);
					break;
				case "helpUrl":
					GetValueString (out helpUrl);
					break;
				case "aliases":
					GetValueStringArray (out aliases);
					break;
				default:
					AddWarning (defPropVal ?? customTypeValueObj, $"Custom type value '{customTypeValueName}' definition has unknown property '{defPropName}'");
					break;
				}
			}

			return new CustomTypeValue (customTypeValueName, description, deprecationMessage, helpUrl, aliases);
		}

		static readonly MSBuildValueKind[] PermittedBaseKinds = new[] {
			MSBuildValueKind.Guid,
			MSBuildValueKind.Int,
			MSBuildValueKind.WarningCode
		};

		public static string? GetErrorIfInvalidCustomTypeDisplayName (string name)
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

		public static string? GetErrorIfInvalidMSBuildIdentifier (string identifier)
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
