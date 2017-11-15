// Copyright (c) Microsoft. All rights reserved.
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
				if (!att.IsAbstract) {
					yield return att;
				}
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

		static IEnumerable<BaseInfo> GetAbstractAttributes (this IEnumerable<IMSBuildSchema> schemas, MSBuildKind kind, string elementName)
		{
			switch (kind) {
			case MSBuildKind.Item:
				return schemas.GetItems ();
			case MSBuildKind.Task:
				return schemas.GetTasks ();
			case MSBuildKind.Property:
				return schemas.GetProperties (false);
			case MSBuildKind.Metadata:
				return schemas.GetItemMetadata (elementName, false);
			}
			return null;
		}

		public static IEnumerable<BaseInfo> GetElementCompletions (this MSBuildResolveResult rr, IEnumerable<IMSBuildSchema> schemas)
		{
			if (rr?.LanguageElement == null) {
				yield return MSBuildLanguageElement.Get ("Project");
				yield break;
			}

			foreach (var c in rr.LanguageElement.Children) {
				if (c.IsAbstract) {
					var abstractChildren = GetAbstractChildren (schemas, rr.LanguageElement.AbstractChild.Kind, rr.ElementName);
					if (abstractChildren != null) {
						foreach (var child in abstractChildren) {
							yield return child;
						}
					}
				} else {
					yield return c;
				}
			}
		}

		static IEnumerable<BaseInfo> GetAbstractChildren (this IEnumerable<IMSBuildSchema> schemas, MSBuildKind kind, string elementName)
		{
			switch (kind) {
			case MSBuildKind.Item:
				return schemas.GetItems ();
			case MSBuildKind.Task:
				return schemas.GetTasks ();
			case MSBuildKind.Property:
				return schemas.GetProperties (false);
			case MSBuildKind.Metadata:
				return schemas.GetItemMetadata (elementName, false);
			}
			return null;
		}

		static IReadOnlyList<BaseInfo> GetValueCompletions (MSBuildValueKind kind)
		{
			switch (kind) {
			case MSBuildValueKind.Bool:
				return new BaseInfo [] {
					new ValueInfo ("True", null),
					new ValueInfo ("False", null),
				};
			case MSBuildValueKind.TaskArchitecture:
				return new BaseInfo [] {
					new ValueInfo ("*", null),
					new ValueInfo ("CurrentArchitecture", null),
					new ValueInfo ("x86", null),
					new ValueInfo ("x64", null),
				};
			case MSBuildValueKind.TaskRuntime:
				return new BaseInfo [] {
					new ValueInfo ("*", null),
					new ValueInfo ("CurrentRuntime", null),
					new ValueInfo ("CLR2", null),
					new ValueInfo ("CLR4", null),
				};
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

			var att = rr.LanguageElement.GetAttribute (rr.AttributeName);
			if (att != null) {
				var vals = GetValueCompletions (att.ValueKind);
				if (vals != null) {
					valueSeparators = null;
					return vals;
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

			if (rr.LanguageElement.Kind == MSBuildKind.Metadata) {
				var metadata = schemas.GetMetadata (rr.ParentName, rr.ElementName, false);
				if (metadata?.Values != null) {
					valueSeparators = metadata.ValueSeparators;
					return metadata.Values;
				}
			}

			var vals = GetValueCompletions (rr.LanguageElement.ValueKind);
			if (vals != null) {
				valueSeparators = null;
				return vals;
			}

			valueSeparators = null;
			return Array.Empty<BaseInfo> ();
		}

		public static BaseInfo GetResolvedInfo (this MSBuildResolveResult rr, IEnumerable<IMSBuildSchema> schemas)
		{
			switch (rr.ReferenceKind) {
			case MSBuildKind.ItemReference:
				return schemas.GetItem (rr.ReferenceName);
			case MSBuildKind.MetadataReference:
				return schemas.GetMetadata (rr.ReferenceItemName, rr.ReferenceName, true);
			case MSBuildKind.PropertyReference:
				return schemas.GetProperty (rr.ReferenceName);
			}
			return null;
		}
	}
}
