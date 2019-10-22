// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using MonoDevelop.MSBuild.Language;

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
					if (!found.TryGetValue (item.Key, out ItemInfo existing) || (existing.Description.IsEmpty && !item.Value.Description.IsEmpty)) {
						found [item.Key] = item.Value;
					}
				}
			}

			return found.Values;
		}

		public static ItemInfo GetItem (this IEnumerable<IMSBuildSchema> schemas, string name)
		{
			// prefer items with descriptions, in case items that just add metadata are found first
			return schemas.GetAllItemDefinitions (name).GetFirstWithDescriptionOrDefault ();
		}

		//collect all known definitions for this item
		static IEnumerable<ItemInfo> GetAllItemDefinitions (this IEnumerable<IMSBuildSchema> schemas, string name)
		{
			foreach (var schema in schemas) {
				if (schema.Items.TryGetValue (name, out ItemInfo item)) {
					yield return item;
				}
			}
		}

		//collect known metadata for this item across all imports
		public static IEnumerable<MetadataInfo> GetMetadata (this IEnumerable<IMSBuildSchema> schemas, string itemName, bool includeBuiltins)
		{
			if (includeBuiltins) {
				foreach (var b in Builtins.Metadata) {
					yield return b.Value;
				}
			}

			if (itemName == null) {
				yield break;
			}

			var names = new HashSet<string> (Builtins.Metadata.Keys, StringComparer.OrdinalIgnoreCase);
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
			if (includeBuiltins && Builtins.Metadata.TryGetValue (metadataName, out MetadataInfo builtinMetaInfo)) {
				yield return builtinMetaInfo;
			}

			foreach (var item in schemas.GetAllItemDefinitions (itemName)) {
				if (item.Metadata.TryGetValue (metadataName, out MetadataInfo metaInfo)) {
					yield return metaInfo;
				}
			}
		}

		public static MetadataInfo GetMetadata (this IEnumerable<IMSBuildSchema> schemas, string itemName, string metadataName, bool includeBuiltins)
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
				if (schema.Tasks.TryGetValue (taskName, out TaskInfo task)) {
					if (!task.IsInferred) {
						yield return task;
					}
				}
			}

			foreach (var schema in schemas) {
				if (schema.Tasks.TryGetValue (taskName, out TaskInfo task)) {
					if (task.IsInferred) {
						yield return task;
					}
				}
			}
		}

		public static TaskInfo GetTask (this IEnumerable<IMSBuildSchema> schemas, string name)
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
				if (task.Parameters.TryGetValue (parameterName, out TaskParameterInfo parameter)) {
					yield return parameter;
				}
			}
		}

		public static TaskParameterInfo GetTaskParameter (this IEnumerable<IMSBuildSchema> schemas, string taskName, string parameterName)
		{
			return schemas.GetAllTaskParameterVariants (taskName, parameterName).FirstOrDefault ();
		}

		public static IEnumerable<PropertyInfo> GetProperties (this IEnumerable<IMSBuildSchema> schemas, bool includeBuiltins)
		{
			if (includeBuiltins) {
				foreach (var b in Builtins.Properties) {
					yield return b.Value;
				}
			}

			bool showPrivateSymbols = MSBuildHost.Options.ShowPrivateSymbols;
			var names = new HashSet<string> (Builtins.Properties.Keys, StringComparer.OrdinalIgnoreCase);
			foreach (var schema in schemas) {
				foreach (var item in schema.Properties) {
					if ((showPrivateSymbols || !schema.IsPrivate (item.Key)) && names.Add (item.Key)) {
						yield return item.Value;
					}
				}
			}
		}

		public static IEnumerable<PropertyInfo> GetAllPropertyVariants (this IEnumerable<IMSBuildSchema> schemas, string propertyName)
		{
			foreach (var schema in schemas) {
				if (schema.Properties.TryGetValue (propertyName, out PropertyInfo property)) {
					yield return property;
				}
			}
		}

		public static PropertyInfo GetProperty (this IEnumerable<IMSBuildSchema> schemas, string name)
		{
			return schemas.GetAllPropertyVariants (name).FirstOrDefault ();
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
				if (schema.Targets.TryGetValue (targetName, out TargetInfo target)) {
					yield return target;
				}
			}
		}

		public static TargetInfo GetTarget (this IEnumerable<IMSBuildSchema> schemas, string targetName)
		{
			return schemas.GetAllTargetVariants (targetName).FirstOrDefault ();
		}

		public static ValueInfo GetAttributeInfo (this IEnumerable<IMSBuildSchema> schemas,  MSBuildAttributeSyntax attribute, string elementName, string attributeName)
		{
			if (attribute.IsAbstract) {
				switch (attribute.AbstractKind.Value) {
				case MSBuildSyntaxKind.Parameter:
					return schemas.GetTaskParameter (elementName, attributeName);
				case MSBuildSyntaxKind.Metadata:
					return schemas.GetMetadata (elementName, attributeName, false);
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
					item?.ValueKind ?? MSBuildValueKind.Unknown,
					attribute.Required, attribute.AbstractKind
				);
			}

			return attribute;
		}

		public static ValueInfo GetElementInfo (this IEnumerable<IMSBuildSchema> schemas, MSBuildElementSyntax element, string parentName, string elementName, bool omitEmpty = false)
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
					return schemas.GetMetadata (parentName, elementName, false);
				case MSBuildSyntaxKind.Property:
					return schemas.GetProperty (elementName);
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
			return schemas.OfType<MSBuildDocument> ().SelectMany (d => d.Configurations).Distinct ();
		}

		public static IEnumerable<string> GetPlatforms (this IEnumerable<IMSBuildSchema> schemas)
		{
			return schemas.OfType<MSBuildDocument> ().SelectMany (d => d.Platforms).Distinct ();
		}

		static T GetFirstWithDescriptionOrDefault<T> (this IEnumerable<T> seq) where T : BaseInfo
		{
			T first = null;

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
	}
}
