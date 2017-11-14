// Copyright (c) Microsoft. ALl rights reserved.
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
		public static IEnumerable<MetadataInfo> GetItemMetadata (this IEnumerable<IMSBuildSchema> schemas, string itemName, bool includeBuiltins)
		{
			if (includeBuiltins) {
				foreach (var b in Builtins.Metadata) {
					yield return b.Value;
				}
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

		static IEnumerable<TaskInfo> GetAllTaskDefinitions (this IEnumerable<IMSBuildSchema> schemas, string taskName)
		{
			foreach (var schema in schemas) {
				if (schema.Tasks.TryGetValue (taskName, out TaskInfo task)) {
					yield return task;
				}
			}
		}

		public static TaskInfo GetTask (this IEnumerable<IMSBuildSchema> schemas, string name)
		{
			return schemas.GetAllTaskDefinitions (name).FirstOrDefault ();
		}

		public static IEnumerable<TaskParameterInfo> GetTaskParameters (this IEnumerable<IMSBuildSchema> schemas, string taskName)
		{
			var names = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
			foreach (var task in schemas.GetAllTaskDefinitions (taskName)) {
				foreach (var parameter in task.Parameters) {
					if (names.Add (parameter.Key)) {
						yield return parameter.Value;
					}
				}
			}
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

		public static IEnumerable<PropertyInfo> GetAllPropertyDefinitions (this IEnumerable<IMSBuildSchema> schemas, string propertyName)
		{
			foreach (var schema in schemas) {
				if (schema.Properties.TryGetValue (propertyName, out PropertyInfo property)) {
					yield return property;
				}
			}
		}

		public static PropertyInfo GetProperty (this IEnumerable<IMSBuildSchema> schemas, string name)
		{
			return schemas.GetAllPropertyDefinitions (name).FirstOrDefault ();
		}
	}
}
