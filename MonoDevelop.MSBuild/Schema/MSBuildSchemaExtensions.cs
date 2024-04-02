// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP
#nullable enable
#else
#nullable enable annotations
#endif

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Syntax;
using MonoDevelop.MSBuild.Language.Typesystem;

namespace MonoDevelop.MSBuild.Schema
{
	static class MSBuildSchemaExtensions
	{
		public static IEnumerable<ItemInfo> GetItems (this IEnumerable<IMSBuildSchema> schemas)
		{
			// prefer items with descriptions, in case items that just add metadata are found first
			// this means we can't do it lazily. we could defer items without descriptions till
			// the end and discard them when we find a version with a description, which would be
			// partially lazy, but probably not worth it
			var found = new Dictionary<string, ItemInfo> (StringComparer.OrdinalIgnoreCase);

			bool hidePrivateSymbols = !MSBuildHost.Options.ShowPrivateSymbols;
			foreach (var schema in schemas) {
				foreach (var item in schema.Items) {
					if (hidePrivateSymbols && schema.IsPrivate (item.Key)) {
						continue;
					}
					if (!found.TryGetValue (item.Key, out ItemInfo? existing) || (existing.Description.IsEmpty && !item.Value.Description.IsEmpty)) {
						found[item.Key] = item.Value;
					}
				}
			}

			return found.Values;
		}

		public static ItemInfo? GetItem (this IEnumerable<IMSBuildSchema> schemas, string name)
		{
			// prefer items with descriptions, in case items that just add metadata are found first
			return schemas.GetAllItemDefinitions (name).GetFirstWithDescriptionOrDefault ();
		}

		//collect all known definitions for this item
		static IEnumerable<ItemInfo> GetAllItemDefinitions (this IEnumerable<IMSBuildSchema> schemas, string name)
		{
			foreach (var schema in schemas) {
				if (schema.Items.TryGetValue (name, out ItemInfo? item)) {
					yield return item;
				}
			}
		}

		//collect known metadata for this item across all imports
		public static IEnumerable<MetadataInfo> GetMetadata (this IEnumerable<IMSBuildSchema> schemas, string itemName, bool includeBuiltins)
		{
			if (includeBuiltins) {
				foreach (var b in MSBuildIntrinsics.Metadata) {
					yield return b.Value;
				}
			}

			if (itemName == null) {
				yield break;
			}

			var names = new HashSet<string> (MSBuildIntrinsics.Metadata.Keys, StringComparer.OrdinalIgnoreCase);
			foreach (var item in schemas.GetAllItemDefinitions (itemName)) {
				foreach (var m in item.Metadata) {
					if (names.Add (m.Key)) {
						yield return m.Value;
					}
				}
			}
		}

		//collect all known definitions for this metadata
		static IEnumerable<MetadataInfo> GetAllMetadataDefinitions (this IEnumerable<IMSBuildSchema> schemas, string itemName, string metadataName, bool includeBuiltins)
		{
			if (includeBuiltins && MSBuildIntrinsics.Metadata.TryGetValue (metadataName, out MetadataInfo? builtinMetaInfo)) {
				yield return builtinMetaInfo;
			}

			if (itemName == null) {
				yield break;
			}

			foreach (var item in schemas.GetAllItemDefinitions (itemName)) {
				if (item.Metadata.TryGetValue (metadataName, out MetadataInfo? metaInfo)) {
					yield return metaInfo;
				}
			}
		}

		public static MetadataInfo? GetMetadata (this IEnumerable<IMSBuildSchema> schemas, string itemName, string metadataName, bool includeBuiltins)
		{
			return schemas.GetAllMetadataDefinitions (itemName, metadataName, includeBuiltins).FirstOrDefault ();
		}

		public static IEnumerable<TaskInfo> GetTasks (this IEnumerable<IMSBuildSchema> schemas)
		{
			var names = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
			foreach (var schema in schemas) {
				foreach (var task in schema.Tasks) {
					if (names.Add (task.Key)) {
						yield return task.Value;
					}
				}
			}
		}

		static IEnumerable<TaskInfo> GetAllTaskVariants (this IEnumerable<IMSBuildSchema> schemas, string taskName)
		{
			//definitions take precedence over inference
			foreach (var schema in schemas) {
				if (schema.Tasks.TryGetValue (taskName, out TaskInfo? task)) {
					if (task.DeclarationKind != TaskDeclarationKind.Inferred) {
						yield return task;
					}
				}
			}

			foreach (var schema in schemas) {
				if (schema.Tasks.TryGetValue (taskName, out TaskInfo? task)) {
					if (task.DeclarationKind == TaskDeclarationKind.Inferred) {
						yield return task;
					}
				}
			}
		}

		public static TaskInfo? GetTask (this IEnumerable<IMSBuildSchema> schemas, string name)
		{
			return schemas.GetAllTaskVariants (name).FirstOrDefault ();
		}

		public static IEnumerable<TaskParameterInfo> GetTaskParameters (this IEnumerable<IMSBuildSchema> schemas, string taskName)
		{
			var names = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
			foreach (var task in schemas.GetAllTaskVariants (taskName)) {
				foreach (var parameter in task.Parameters) {
					if (names.Add (parameter.Key)) {
						yield return parameter.Value;
					}
				}
			}
		}

		public static IEnumerable<TaskParameterInfo> GetAllTaskParameterVariants (this IEnumerable<IMSBuildSchema> schemas, string taskName, string parameterName)
		{
			var names = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
			foreach (var task in schemas.GetAllTaskVariants (taskName)) {
				if (task.Parameters.TryGetValue (parameterName, out TaskParameterInfo? parameter)) {
					yield return parameter;
				}
			}
		}

		public static TaskParameterInfo? GetTaskParameter (this IEnumerable<IMSBuildSchema> schemas, string taskName, string parameterName)
		{
			return schemas.GetAllTaskParameterVariants (taskName, parameterName).FirstOrDefault ();
		}

		public static IEnumerable<PropertyInfo> GetProperties (this IEnumerable<IMSBuildSchema> schemas, bool includeReadOnly)
		{
			foreach (var prop in MSBuildIntrinsics.Properties.Values) {
				if (includeReadOnly || !prop.IsReadOnly) {
					yield return prop;
				}
			}

			bool showPrivateSymbols = MSBuildHost.Options.ShowPrivateSymbols;
			var names = new HashSet<string> (MSBuildIntrinsics.Properties.Keys, StringComparer.OrdinalIgnoreCase);
			foreach (var schema in schemas) {
				foreach (var item in schema.Properties) {
					if ((showPrivateSymbols || !schema.IsPrivate (item.Key)) && names.Add (item.Key)) {
						yield return item.Value;
					}
				}
			}
		}

		public static IEnumerable<PropertyInfo> GetAllPropertyVariants (
			this IEnumerable<IMSBuildSchema> schemas, string propertyName, bool includeBuiltins)
		{
			if (includeBuiltins && MSBuildIntrinsics.Properties.TryGetValue (propertyName, out var b)) {
				yield return b;
			}
			foreach (var schema in schemas) {
				if (schema.Properties.TryGetValue (propertyName, out PropertyInfo? property)) {
					yield return property;
				}
			}
		}

		public static PropertyInfo? GetProperty (this IEnumerable<IMSBuildSchema> schemas, string name, bool includeBuiltins)
		{
			return schemas.GetAllPropertyVariants (name, includeBuiltins).FirstOrDefault ();
		}

		public static IEnumerable<TargetInfo> GetTargets (this IEnumerable<IMSBuildSchema> schemas)
		{
			bool showPrivateSymbols = MSBuildHost.Options.ShowPrivateSymbols;
			var names = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
			foreach (var schema in schemas) {
				foreach (var target in schema.Targets) {
					if ((showPrivateSymbols || !schema.IsPrivate (target.Key)) && names.Add (target.Key)) {
						yield return target.Value;
					}
				}
			}
		}

		public static IEnumerable<TargetInfo> GetAllTargetVariants (this IEnumerable<IMSBuildSchema> schemas, string targetName)
		{
			foreach (var schema in schemas) {
				if (schema.Targets.TryGetValue (targetName, out TargetInfo? target)) {
					yield return target;
				}
			}
		}

		public static TargetInfo? GetTarget (this IEnumerable<IMSBuildSchema> schemas, string targetName)
		{
			return schemas.GetAllTargetVariants (targetName).FirstOrDefault ();
		}

		public static ITypedSymbol GetAttributeInfo (this IEnumerable<IMSBuildSchema> schemas, MSBuildAttributeSyntax attribute, string elementName, string attributeName)
		{
			if (attribute.IsAbstract) {
				switch (attribute.AbstractKind.Value) {
				case MSBuildSyntaxKind.Parameter:
					if (schemas.GetTaskParameter (elementName, attributeName) is TaskParameterInfo taskParameterInfo) {
						return taskParameterInfo;
					}
					break;
				case MSBuildSyntaxKind.Metadata:
					if (schemas.GetMetadata (elementName, attributeName, false) is MetadataInfo metadataInfo) {
						return metadataInfo;
					}
					break;
				default:
					throw new InvalidOperationException ($"Unsupported abstract attribute kind {attribute.AbstractKind}");
				}
			}

			return schemas.SpecializeAttribute (attribute, elementName);
		}

		public static MSBuildAttributeSyntax SpecializeAttribute (this IEnumerable<IMSBuildSchema> schemas, MSBuildAttributeSyntax attribute, string elementName)
		{
			if (attribute.ValueKind == MSBuildValueKind.MatchItem) {
				var item = schemas.GetItem (elementName);
				return new MSBuildAttributeSyntax (
					attribute.Element, attribute.Name, attribute.Description,
					attribute.SyntaxKind,
					item?.ValueKind ?? MSBuildValueKind.UnknownItem.AsList (),
					item?.CustomType,
					attribute.Required
				);
			}

			return attribute;
		}

		public static ITypedSymbol? GetElementInfo (this IEnumerable<IMSBuildSchema> schemas, MSBuildElementSyntax element, string? parentName, string elementName, bool omitEmpty = false)
		{
			if (element.IsAbstract) {
				switch (element.SyntaxKind) {
				case MSBuildSyntaxKind.Item:
				case MSBuildSyntaxKind.ItemDefinition:
					if (omitEmpty) {
						return null;
					}
					return schemas.GetItem (elementName);
				case MSBuildSyntaxKind.Metadata:
					if (parentName is not null) {
						return schemas.GetMetadata (parentName, elementName, false);
					}
					break;
				case MSBuildSyntaxKind.Property:
					return schemas.GetProperty (elementName, false);
				case MSBuildSyntaxKind.Task:
					return null;
				case MSBuildSyntaxKind.Parameter:
					if (omitEmpty) {
						return null;
					}
					return new TaskParameterInfo (elementName, null, false, false, MSBuildValueKind.Unknown);
				default:
					throw new InvalidOperationException ($"Unsupported abstract element kind {element.SyntaxKind}");
				}
			}

			if (omitEmpty && (element.ValueKind == MSBuildValueKind.Nothing || element.ValueKind == MSBuildValueKind.Data)) {
				return null;
			}
			return element;
		}

		public static IEnumerable<string> GetConfigurations (this IEnumerable<IMSBuildSchema> schemas)
		{
			return schemas.OfType<MSBuildInferredSchema> ().SelectMany (d => d.Configurations).Distinct ();
		}

		public static IEnumerable<string> GetPlatforms (this IEnumerable<IMSBuildSchema> schemas)
		{
			return schemas.OfType<MSBuildInferredSchema> ().SelectMany (d => d.Platforms).Distinct ();
		}

		static T? GetFirstWithDescriptionOrDefault<T> (this IEnumerable<T> seq) where T : class, ISymbol
		{
			T? first = null;

			//prefer infos with descriptions, in case non-schema infos (or item infos that
			//just add metadata) are found first
			foreach (var info in seq) {
				if (info.Description.Text != null) {
					return info;
				}
				return first ?? info;
			}
			return null;
		}

		public static IEnumerable<CustomTypeInfo> GetDerivedTypes (this IEnumerable<IMSBuildSchema> schemas, MSBuildValueKind baseKind)
		{
			foreach (var schema in schemas) {
				foreach (var type in schema.Types) {
					if (type.Value.BaseKind == baseKind) {
						yield return type.Value;
					}
				}
			}
		}

		public static IEnumerable<CustomTypeValue> GetWarningCodeValues (this IEnumerable<IMSBuildSchema> schemas)
		{
			var set = new HashSet<string> (StringComparer.OrdinalIgnoreCase);

			foreach (var schema in schemas) {
				foreach (var typeKV in schema.Types) {
					var type = typeKV.Value;
					if (type.BaseKind == MSBuildValueKind.WarningCode) {
						foreach (var value in type.Values) {
							if (set.Add (value.Name)) {
								yield return value;
							}
						}
					}
				}
			}
		}

		public static IEnumerable<CustomTypeInfo> GetTypeByName (this IEnumerable<IMSBuildSchema> schemas, string typeName)
		{
			foreach (var schema in schemas) {
				if (schema.Types.TryGetValue (typeName, out CustomTypeInfo? customType)) {
					yield return customType;
				}
			}
		}

		/// <summary>
		/// Try to get known values for the values described by `valueDescriptor`.
		/// If the descriptor is unresolved, then `inferredKind` will be used, and is assumed to be a kind that has been inferred from the name.
		/// </summary>
		/// <param name="kindIfUnknown">
		/// Optionally provide an alternate <see cref="MSBuildValueKind"/> to be used if the the <see cref="ITypedSymbol"/>'s value kind is <see cref="MSBuildValueKind.Unknown"/>.
		/// </param>
		public static bool TryGetKnownValues (
			this IEnumerable<IMSBuildSchema> schema, ITypedSymbol valueDescriptor, [NotNullWhen (true)] out IEnumerable<ISymbol>? values,
			MSBuildValueKind kindIfUnknown = MSBuildValueKind.Unknown)
		{
			var kind = valueDescriptor.ValueKindWithoutModifiers ();

			// FIXME: This is a temporary hack so we have completion for imported XSD schemas with missing type info.
			// It is not needed for inferred schemas, as they have already performed the inference.
			if (kind == MSBuildValueKind.Unknown) {
				kind = kindIfUnknown;
			}

			if (kind == MSBuildValueKind.CustomType) {
				if (valueDescriptor?.CustomType?.Values is IReadOnlyList<ISymbol> customTypeValues) {
					values = customTypeValues;
					return true;
				}
			}
			else if (kind == MSBuildValueKind.WarningCode) {
				// this should almost never be null, so don't bother checking whether it's empty
				values = schema.GetWarningCodeValues ();
				return true;
			}
			else {
				var simpleValues = kind.GetSimpleValues ();
				if (simpleValues.Count > 0) {
					values = simpleValues;
					return true;
				}
			}

			values = null;
			return false;
		}

		/// <summary>
		/// Try to get resolve a known value with the given name. The value will be resolved from the type of the `valueDescriptor`.
		/// If the descriptor is unresolved, then `inferredKind` will be used, and is assumed to be a kind that has been inferred from the name.
		/// </summary>
		/// <param name="isError">True if the type supports value resolution but the value was not resolved</param>
		public static bool TryGetKnownValue (this IEnumerable<IMSBuildSchema> schema, ITypedSymbol valueDescriptor, string value, [NotNullWhen (true)] out ISymbol? resolvedValue, out bool isError)
		{
			if (!schema.TryGetKnownValues (valueDescriptor, out IEnumerable<ISymbol>? knownValues)) {
				resolvedValue = null;
				isError = false;
				return false;
			}

			var valueComparer = (valueDescriptor?.CustomType?.CaseSensitive ?? false) ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
			foreach (var kv in knownValues) {
				if (string.Equals (kv.Name, value, valueComparer)) {
					resolvedValue = kv;
					isError = false;
					return true;
				}
				if (kv is CustomTypeValue cv && cv.Aliases is not null) {
					foreach (var alias in cv.Aliases) {
						if (string.Equals (alias, value, valueComparer)) {
							resolvedValue = kv;
							isError = false;
							return true;
						}
					}
				}
			}

			resolvedValue = null;
			isError = true;
			return false;
		}
	}
}
