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
			bool isInTarget = false;
			if (rr.LanguageElement.Kind == MSBuildKind.Item) {
				isInTarget = rr.LanguageElement.IsInTarget (rr.XElement);
			}

			foreach (var att in rr.LanguageElement.Attributes) {
				if (!att.IsAbstract) {
					if (rr.LanguageElement.Kind == MSBuildKind.Item) {
						if (isInTarget) {
							if (att.Name == "Update") {
								continue;
							}
						} else {
							if (att.Name == "KeepMetadata" || att.Name == "RemoveMetadata" || att.Name == "KeepDuplicates") {
								continue;
							}
						}
					}
					yield return att;
				}
			}


			if (rr.LanguageElement.Kind == MSBuildKind.Item && tv.IsAtLeast (MSBuildToolsVersion.V15_0)) {
				foreach (var item in schemas.GetMetadata (rr.ElementName, false)) {
					yield return item;
				}
			}

			if (rr.LanguageElement.Kind == MSBuildKind.Task) {
				foreach (var parameter in schemas.GetTaskParameters (rr.ElementName)) {
					yield return parameter;

				}
			}
		}

		public static bool IsInTarget (this MSBuildLanguageElement resolvedElement, Xml.Dom.XElement element)
		{
			switch (resolvedElement.Kind) {
			case MSBuildKind.Metadata:
				element = element?.ParentElement ();
				goto case MSBuildKind.Item;
			case MSBuildKind.Property:
			case MSBuildKind.Item:
				element = element?.ParentElement ();
				goto case MSBuildKind.ItemGroup;
			case MSBuildKind.ItemGroup:
			case MSBuildKind.PropertyGroup:
				var name = element?.ParentElement ()?.Name.Name;
				return string.Equals (name, "Target", StringComparison.OrdinalIgnoreCase);
			}
			return false;
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
				return schemas.GetMetadata (elementName, false);
			}
			return null;
		}

		public static IEnumerable<BaseInfo> GetElementCompletions (this MSBuildResolveResult rr, IEnumerable<IMSBuildSchema> schemas)
		{
			if (rr?.LanguageElement == null) {
				yield return MSBuildLanguageElement.Get ("Project");
				yield break;
			}

			if (rr.LanguageElement.Children == null) {
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
				return schemas.GetMetadata (elementName, false);
			}
			return null;
		}

		public static IReadOnlyList<BaseInfo> GetValueCompletions (
			MSBuildValueKind kind, IEnumerable<IMSBuildSchema> schemas,
			IReadOnlyList<FrameworkReference> tfms)
		{
			var simple = kind.GetSimpleValues (true);
			if (simple != null) {
				return simple;
			}

			switch (kind) {
			case MSBuildValueKind.TargetName:
				return schemas.GetTargets ().ToList ();
			case MSBuildValueKind.PropertyName:
				return schemas.GetProperties (true).ToList ();
			case MSBuildValueKind.ItemName:
				return schemas.GetItems ().ToList ();
			case MSBuildValueKind.TargetFramework:
				return FrameworkInfoProvider.Instance.GetFrameworksWithShortNames ().ToList ();
			case MSBuildValueKind.TargetFrameworkIdentifier:
				return FrameworkInfoProvider.Instance.GetFrameworkIdentifiers ().ToList ();
			case MSBuildValueKind.TargetFrameworkVersion:
				return tfms.SelectMany (
					tfm => FrameworkInfoProvider.Instance.GetFrameworkVersions (tfm.Identifier)
				).ToList ();
			case MSBuildValueKind.TargetFrameworkProfile:
				return tfms.SelectMany (
					tfm => FrameworkInfoProvider.Instance.GetFrameworkProfiles (tfm.Identifier, tfm.Version)
				).ToList ();
			}
			return null;
		}

		public static BaseInfo GetResolvedReference (this MSBuildResolveResult rr, IEnumerable<IMSBuildSchema> schemas, IReadOnlyList<FrameworkReference> tfms)
		{
			switch (rr.ReferenceKind) {
			case MSBuildReferenceKind.Item:
				return schemas.GetItem (rr.ReferenceName);
			case MSBuildReferenceKind.Metadata:
				return schemas.GetMetadata (rr.ReferenceItemName, rr.ReferenceName, true);
			case MSBuildReferenceKind.Property:
				return schemas.GetProperty (rr.ReferenceName);
			case MSBuildReferenceKind.Task:
				return schemas.GetTask (rr.ReferenceName);
			case MSBuildReferenceKind.Target:
				return schemas.GetTarget (rr.ReferenceName);
			case MSBuildReferenceKind.Keyword:
				var attName = rr.AttributeName;
				if (attName != null) {
					var att = rr.LanguageElement.GetAttribute (attName);
					if (att != null && !att.IsAbstract) {
						return att;
					}
				} else {
					if (!rr.LanguageElement.IsAbstract) {
						return rr.LanguageElement;
					}
				}
				break;
			case MSBuildReferenceKind.KnownValue:
				return rr.ReferenceValue;
			case MSBuildReferenceKind.TargetFramework:
				var fx = (FrameworkReference)rr.ReferenceObject;
				return FrameworkInfoProvider.Instance.GetBestInfo (fx, tfms);
			}
			return null;
		}

		public static ValueInfo GetElementOrAttributeValueInfo (this MSBuildResolveResult rr, IEnumerable<IMSBuildSchema> schemas)
		{
			if (rr.LanguageElement == null) {
				return null;
			}

			if (rr.AttributeName != null) {
				return schemas.GetAttributeInfo (rr.LanguageAttribute, rr.ElementName, rr.AttributeName);
			}

			return schemas.GetElementInfo (rr.LanguageElement, rr.ParentName, rr.ElementName);
		}

		public static MSBuildValueKind InferValueKindIfUnknown (ValueInfo variable)
		{
			var kind = InferUnknownKind (variable);

			if (variable.ValueSeparators != null) {
				if (variable.ValueSeparators.Contains (';')) {
					kind |= MSBuildValueKind.List;
				}
				if (variable.ValueSeparators.Contains (',')) {
					kind |= MSBuildValueKind.CommaList;
				}
			}

			return kind;
		}

		static MSBuildValueKind InferUnknownKind (ValueInfo variable)
		{
			if (variable.ValueKind != MSBuildValueKind.Unknown) {
				return variable.ValueKind;
			}

			if (variable is PropertyInfo || variable is MetadataInfo) {
				if (StartsWith ("Enable")
					|| StartsWith ("Disable")
					|| StartsWith ("Require")
					|| StartsWith ("Use")
					|| StartsWith ("Allow")
					|| EndsWith ("Enabled")
					|| EndsWith ("Disabled")
					|| EndsWith ("Required")) {
					return MSBuildValueKind.Bool;
				}
				if (EndsWith ("DependsOn")) {
					return MSBuildValueKind.TargetName.List ();
				}
				if (EndsWith ("Path")) {
					return MSBuildValueKind.FileOrFolder;
				}
				if (EndsWith ("Paths")) {
					return MSBuildValueKind.FileOrFolder.List ();
				}
				if (EndsWith ("Directory")
					|| EndsWith ("Dir")) {
					return MSBuildValueKind.Folder;
				}
				if (EndsWith ("File")) {
					return MSBuildValueKind.File;
				}
				if (EndsWith ("FileName")) {
					return MSBuildValueKind.Filename;
				}
				if (EndsWith ("Url")) {
					return MSBuildValueKind.Url;
				}
				if (EndsWith ("Ext")) {
					return MSBuildValueKind.Extension;
				}
				if (EndsWith ("Guid")) {
					return MSBuildValueKind.Guid;
				}
				if (EndsWith ("Directories") || EndsWith ("Dirs")) {
					return MSBuildValueKind.Folder.List ();
				}
				if (EndsWith ("Files")) {
					return MSBuildValueKind.File.List ();
				}
			}

			return MSBuildValueKind.Unknown;

			bool StartsWith (string prefix) => variable.Name.StartsWith (prefix, StringComparison.OrdinalIgnoreCase)
			                                           && variable.Name.Length > prefix.Length
			                                           && char.IsUpper (variable.Name[prefix.Length]);
			bool EndsWith (string suffix) => variable.Name.EndsWith (suffix, StringComparison.OrdinalIgnoreCase);
		}
	}
}
