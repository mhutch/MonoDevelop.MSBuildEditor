// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MonoDevelop.MSBuildEditor.Evaluation;
using MonoDevelop.MSBuildEditor.Language;
using System.Text;

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
				var spat = schemas.SpecializeAttribute (att, rr.ElementName);
				if (!spat.IsAbstract) {
					if (rr.LanguageElement.Kind == MSBuildKind.Item) {
						if (isInTarget) {
							if (spat.Name == "Update") {
								continue;
							}
						} else {
							if (spat.Name == "KeepMetadata" || spat.Name == "RemoveMetadata" || spat.Name == "KeepDuplicates") {
								continue;
							}
						}
					}
					yield return spat;
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
			MSBuildValueKind kind,
			MSBuildRootDocument doc,
			MSBuildResolveResult rr = null)
		{
			var simple = kind.GetSimpleValues (true);
			if (simple != null) {
				return simple;
			}

			switch (kind) {
			case MSBuildValueKind.TaskOutputParameterName:
				return doc.GetTaskParameters (rr.ParentName).Where (p => p.IsOutput).ToList ();
			case MSBuildValueKind.TargetName:
				return doc.GetTargets ().ToList ();
			case MSBuildValueKind.PropertyName:
				return doc.GetProperties (true).ToList ();
			case MSBuildValueKind.ItemName:
				return doc.GetItems ().ToList ();
			case MSBuildValueKind.TargetFramework:
				return FrameworkInfoProvider.Instance.GetFrameworksWithShortNames ().ToList ();
			case MSBuildValueKind.TargetFrameworkIdentifier:
				return FrameworkInfoProvider.Instance.GetFrameworkIdentifiers ().ToList ();
			case MSBuildValueKind.TargetFrameworkVersion:
				return doc.Frameworks.SelectMany (
					tfm => FrameworkInfoProvider.Instance.GetFrameworkVersions (tfm.Identifier)
				).ToList ();
			case MSBuildValueKind.TargetFrameworkProfile:
				return doc.Frameworks.SelectMany (
					tfm => FrameworkInfoProvider.Instance.GetFrameworkProfiles (tfm.Identifier, tfm.Version)
				).ToList ();
			case MSBuildValueKind.Configuration:
				return doc.GetConfigurations ().Select (c => new ConstantInfo (c, "")).ToList ();
			case MSBuildValueKind.Platform:
				return doc.GetPlatforms ().Select (c => new ConstantInfo (c, "")).ToList ();
			}

			var fileCompletions = GetFilenameCompletions (kind, doc, null, 0);
			if (fileCompletions != null) {
				return fileCompletions;
			}

			return null;
		}

		public static IReadOnlyList<BaseInfo> GetFilenameCompletions (
			MSBuildValueKind kind, MSBuildRootDocument doc,
			ExpressionNode triggerExpression, int triggerLength)
		{
			bool includeFiles = false;
			switch (kind) {
			case MSBuildValueKind.File:
			case MSBuildValueKind.ProjectFile:
				includeFiles = true;
				break;
			case MSBuildValueKind.FileOrFolder:
				includeFiles = true;
				break;
			case MSBuildValueKind.Folder:
			case MSBuildValueKind.FolderWithSlash:
				break;
			default:
				return null;
			}

			var basePaths = EvaluateExpressionAsPaths (triggerExpression, doc, triggerLength + 1).ToList ();
			return basePaths.Count == 0 ? null : GetPathCompletions (doc.Filename, basePaths, includeFiles);
		}

		public static IEnumerable<string> EvaluateExpressionAsPaths (ExpressionNode expression, MSBuildRootDocument doc, int skipEndChars = 0)
		{
			if (expression == null) {
				yield return Path.GetDirectoryName (doc.Filename);
				yield break;
			}

			if (expression is ExpressionLiteral lit) {
				var path = lit.Value.Substring (0, lit.Value.Length - skipEndChars);
				//FIXME handle encoding
				yield return Projects.MSBuild.MSBuildProjectService.FromMSBuildPath (Path.GetDirectoryName (doc.Filename), path);
				yield break;
			}

			if (!(expression is Expression expr)) {
				yield break;
			}

			//FIXME evaluate directly without the MSBuildEvaluationContext
			var sb = new StringBuilder ();
			for (int i = 0; i < expr.Nodes.Count; i++) {
				var node = expr.Nodes [i];
				if (node is ExpressionLiteral l) {
					var val = l.Value;
					if (i == expr.Nodes.Count - 1) {
						val = val.Substring (0, val.Length - skipEndChars);
					}
					sb.Append (val);
				} else if (node is ExpressionProperty p) {
					sb.Append ($"$({p.Name})");
				} else {
					yield break;
				}
			}

			var evalCtx = MSBuildEvaluationContext.Create (doc.RuntimeInformation, doc.Filename, doc.Filename);
			foreach (var variant in evalCtx.EvaluatePathWithPermutation (sb.ToString (), Path.GetDirectoryName (doc.Filename),  null)) {
				yield return variant;
			}
		}

		static IReadOnlyList<BaseInfo> GetPathCompletions (string projectPath, List<string> completionBasePaths, bool includeFiles)
		{
			var infos = new List<BaseInfo> ();

			var projectBaseDir = Path.GetDirectoryName (projectPath);
			foreach (var p in completionBasePaths) {
				var basePath = Path.GetFullPath (Path.Combine (projectBaseDir, p));

				foreach (var e in Directory.GetDirectories (basePath)) {
					var name = Path.GetFileName (e);
					infos.Add (new FileOrFolderInfo (name, true, e));
				}

				if (includeFiles) {
					foreach (var e in Directory.GetFiles (basePath)) {
						var name = Path.GetFileName (e);
						infos.Add (new FileOrFolderInfo (name, false, e));
					}
				}
			}

			return infos;
		}

		public static BaseInfo GetResolvedReference (this MSBuildResolveResult rr, MSBuildRootDocument doc)
		{
			switch (rr.ReferenceKind) {
			case MSBuildReferenceKind.Item:
				return doc.GetItem ((string)rr.Reference);
			case MSBuildReferenceKind.Metadata:
				var meta = (Tuple<string, string>)rr.Reference;
				return doc.GetMetadata (meta.Item1, meta.Item2, true);
			case MSBuildReferenceKind.Property:
				return doc.GetProperty ((string)rr.Reference);
			case MSBuildReferenceKind.Task:
				return doc.GetTask ((string)rr.Reference);
			case MSBuildReferenceKind.Target:
				return doc.GetTarget ((string)rr.Reference);
			case MSBuildReferenceKind.Keyword:
				return (BaseInfo) rr.Reference;
			case MSBuildReferenceKind.KnownValue:
				return (BaseInfo)rr.Reference;
			case MSBuildReferenceKind.TargetFramework:
				var fx = (FrameworkReference)rr.Reference;
				return FrameworkInfoProvider.Instance.GetBestInfo (fx, doc.Frameworks);
			case MSBuildReferenceKind.TaskParameter:
				var tp = (Tuple<string, string>)rr.Reference;
				return doc.GetTaskParameter (tp.Item1, tp.Item2);
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

		public static MSBuildValueKind InferValueKindIfUnknown (this ValueInfo variable)
		{
			if (variable.ValueKind != MSBuildValueKind.Unknown) {
				return variable.ValueKind;
			}

			if (variable is MSBuildLanguageAttribute att) {
				switch (att.Name) {
				case "Include":
				case "Exclude":
				case "Remove":
				case "Update":
					return MSBuildValueKind.File.List ();
				}
			}

			//assume known items are files
			if (variable.ValueKind == MSBuildValueKind.UnknownItem) {
				return MSBuildValueKind.File;
			}

			if (variable.ValueKind == MSBuildValueKind.UnknownItem.List ()) {
				return MSBuildValueKind.File.List ();
			}

			bool isProperty = variable is PropertyInfo;
			if (isProperty || variable is MetadataInfo) {
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

			//make sure these work even if the common targets schema isn't loaded
			if (isProperty) {
				switch (variable.Name.ToLowerInvariant ()) {
				case "configuration":
					return MSBuildValueKind.Configuration;
				case "platform":
					return MSBuildValueKind.Platform;
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
