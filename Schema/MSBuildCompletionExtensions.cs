// Copyright (c) Microsoft. ALl rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using MonoDevelop.MSBuildEditor.Language;

namespace MonoDevelop.MSBuildEditor.Schema
{
	static class MSBuildCompletionExtensions
	{
		public static IEnumerable<BaseInfo> GetAttributeCompletions (this MSBuildResolveResult rr, IEnumerable<IMSBuildSchema> schemas, MSBuildToolsVersion tv)
		{
			foreach (var att in rr.LanguageElement.Attributes) {
				//FIXME: get descriptions on these
				yield return new ValueInfo (att, null);
			}

			if (rr.LanguageElement.Kind == MSBuildKind.Item && tv.IsAtLeast (MSBuildToolsVersion.V15_0)) {
				foreach (var item in schemas.GetItemMetadata (rr.ElementName, false).Where (a => !a.WellKnown)) {
					yield return item;
				}
			}

			if (rr.LanguageElement.Kind == MSBuildKind.Task) {
				foreach (var parameter in schemas.GetTaskParameters (rr.ElementName)) {
					yield return parameter;

				}
			}
		}

		public static IEnumerable<BaseInfo> GetElementCompletions (this MSBuildResolveResult rr, IEnumerable<IMSBuildSchema> schemas)
		{
			if (rr?.LanguageElement == null) {
				//FIXME: get descriptions on these
				yield return new ValueInfo ("Project", null);
				yield break;
			}

			foreach (var c in rr.LanguageElement.Children) {
				//FIXME: get descriptions on these
				yield return new ValueInfo (c, null);
			}

			var schemaChildren = rr.GetSchemaChildren (schemas);
			if (schemaChildren != null) {
				foreach (var child in schemaChildren) {
					yield return child;
				}
			}
		}

		static IEnumerable<BaseInfo> GetSchemaChildren (this MSBuildResolveResult rr, IEnumerable<IMSBuildSchema> schemas)
		{
			if (rr.LanguageElement.ChildType.HasValue) {
				switch (rr.LanguageElement.ChildType.Value) {
				case MSBuildKind.Item:
					return schemas.GetItems ();
				case MSBuildKind.Task:
					return schemas.GetTasks ();
				case MSBuildKind.Property:
					return schemas.GetProperties (false);
				case MSBuildKind.ItemMetadata:
					return schemas.GetItemMetadata (rr.ElementName, false);
				}
			}
			return null;
		}

		public static IReadOnlyList<BaseInfo> GetAttributeValueCompletions (this MSBuildResolveResult rr, IEnumerable<IMSBuildSchema> schemas, MSBuildToolsVersion tv, out char[] valueSeparators)
		{
			if (tv.IsAtLeast (MSBuildToolsVersion.V15_0)) {
				var meta = schemas.GetMetadata (rr.ElementName, rr.AttributeName, false);
				if (meta != null) {
					valueSeparators = meta.ValueSeparators;
					return meta.Values;
				}
			}

			valueSeparators = null;
			return Array.Empty<BaseInfo> ();
		}

		public static IReadOnlyList<BaseInfo> GetElementValueCompletions (this MSBuildResolveResult rr, IEnumerable<IMSBuildSchema> schemas, MSBuildToolsVersion tv, out char[] valueSeparators)
		{
			if (rr.LanguageElement.Kind == MSBuildKind.Property) {
				var prop = schemas.GetProperty (rr.ElementName);
				if (prop?.Values != null) {
					valueSeparators = prop.ValueSeparators;
					return prop.Values;
				}
			}

			if (rr.LanguageElement.Kind == MSBuildKind.ItemMetadata) {
				var metadata = schemas.GetMetadata (rr.ParentName, rr.ElementName, false);
				if (metadata?.Values != null) {
					valueSeparators = metadata.ValueSeparators;
					return metadata.Values;
				}
			}

			valueSeparators = null;
			return Array.Empty<BaseInfo> ();
		}
	}
}
