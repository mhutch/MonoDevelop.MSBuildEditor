// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using MonoDevelop.MSBuildEditor.Language;

namespace MonoDevelop.MSBuildEditor.Schema
{
	static class MSBuildSchemaExtensions
	{
		public static IEnumerable<ItemInfo> GetItems (this IEnumerable<IMSBuildSchema> schemas)
		{
			var names = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
			foreach (var schema in schemas) {
				foreach (var item in schema.Items) {
					if (!schema.IsPrivate (item.Key) && names.Add (item.Key)) {
						yield return item.Value;
					}
				}
			}
		}

		public static ItemInfo GetItem (this IEnumerable<IMSBuildSchema> schemas, string name)
		{
			return schemas.GetAllItemDefinitions (name).FirstOrDefault ();
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

			var names = new HashSet<string> (Builtins.Properties.Keys, StringComparer.OrdinalIgnoreCase);
			foreach (var schema in schemas) {
				foreach (var item in schema.Properties) {
					if (!schema.IsPrivate (item.Key) && names.Add (item.Key)) {
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
			var names = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
			foreach (var schema in schemas) {
				foreach (var target in schema.Targets) {
					if (!schema.IsPrivate (target.Key) && names.Add (target.Key)) {
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

		public static ValueInfo GetAttributeInfo (this IEnumerable<IMSBuildSchema> schemas,  MSBuildLanguageAttribute attribute, string elementName, string attributeName)
		{
			if (attribute.IsAbstract) {
				switch (attribute.AbstractKind.Value) {
				case MSBuildKind.Parameter:
					return schemas.GetTaskParameter (elementName, attributeName);
				case MSBuildKind.Metadata:
					return schemas.GetMetadata (elementName, attributeName, false);
				default:
					throw new InvalidOperationException ($"Unsupported abstract attribute kind {attribute.AbstractKind}");
				}
			}

			if (attribute.ValueKind == MSBuildValueKind.MatchItem) {
				var item = schemas.GetItem (elementName);
				return new MSBuildLanguageAttribute (
					attribute.Name, attribute.Description, item.ValueKind, attribute.Required, attribute.AbstractKind
				);
			}

			return attribute;
		}

		public static ValueInfo GetElementInfo (this IEnumerable<IMSBuildSchema> schemas, MSBuildLanguageElement element, string parentName, string elementName, bool omitEmpty = false)
		{
			if (element.IsAbstract) {
				switch (element.Kind) {
				case MSBuildKind.Item:
				case MSBuildKind.ItemDefinition:
					if (omitEmpty) {
						return null;
					}
					return schemas.GetItem (elementName);
				case MSBuildKind.Metadata:
					return schemas.GetMetadata (parentName, elementName, false);
				case MSBuildKind.Property:
					return schemas.GetProperty (elementName);
				case MSBuildKind.Parameter:
					if (omitEmpty) {
						return null;
					}
					return new TaskParameterInfo (elementName, null, false, false, MSBuildValueKind.Unknown);
				default:
					throw new InvalidOperationException ($"Unsupported abstract element kind {element.Kind}");
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
	}
}
