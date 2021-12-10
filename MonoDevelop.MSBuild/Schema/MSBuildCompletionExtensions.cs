// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using MonoDevelop.MSBuild.Evaluation;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Language.Syntax;
using MonoDevelop.MSBuild.Language.Typesystem;
using MonoDevelop.MSBuild.Util;
using MonoDevelop.Xml.Dom;

using NuGet.Frameworks;

namespace MonoDevelop.MSBuild.Schema
{
	static class MSBuildCompletionExtensions
	{
		public const string WorkloadAutoImportPropsLocatorName = "Microsoft.NET.SDK.WorkloadAutoImportPropsLocator";

		public static IEnumerable<ISymbol> GetAttributeCompletions (this MSBuildResolveResult rr, IEnumerable<IMSBuildSchema> schemas, MSBuildToolsVersion tv)
		{
			bool isInTarget = false;
			if (rr.ElementSyntax.SyntaxKind == MSBuildSyntaxKind.Item) {
				isInTarget = rr.ElementSyntax.IsInTarget (rr.Element);
			}

			foreach (var att in rr.ElementSyntax.Attributes) {
				var spat = schemas.SpecializeAttribute (att, rr.ElementName);
				if (!spat.IsAbstract) {
					if (rr.ElementSyntax.SyntaxKind == MSBuildSyntaxKind.Item) {
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


			if (rr.ElementSyntax.SyntaxKind == MSBuildSyntaxKind.Item && tv.IsAtLeast (MSBuildToolsVersion.V15_0)) {
				foreach (var item in schemas.GetMetadata (rr.ElementName, false)) {
					yield return item;
				}
			}

			if (rr.ElementSyntax.SyntaxKind == MSBuildSyntaxKind.Task) {
				foreach (var parameter in schemas.GetTaskParameters (rr.ElementName)) {
					yield return parameter;

				}
			}
		}

		public static bool IsInTarget (this MSBuildElementSyntax resolvedElement, XElement element)
			=> IsInTarget (resolvedElement, element, out _);

		public static bool IsInTarget (this MSBuildElementSyntax resolvedElement, XElement element, out XElement targetElement)
		{
			switch (resolvedElement.SyntaxKind) {
			case MSBuildSyntaxKind.Metadata:
				element = element?.ParentElement;
				goto case MSBuildSyntaxKind.Item;
			case MSBuildSyntaxKind.Property:
			case MSBuildSyntaxKind.Item:
				element = element?.ParentElement;
				goto case MSBuildSyntaxKind.ItemGroup;
			case MSBuildSyntaxKind.ItemGroup:
			case MSBuildSyntaxKind.PropertyGroup:
				if (element.ParentElement is XElement te && te.NameEquals (MSBuildElementSyntax.Target.Name, true)) {
					targetElement = te;
					return true;
				}
				break;
			}
			targetElement = null;
			return false;
		}

		public static IEnumerable<ISymbol> GetElementCompletions (this IEnumerable<IMSBuildSchema> schemas,
			MSBuildElementSyntax languageElement, string elementName)
		{
			if (languageElement == null) {
				yield return MSBuildElementSyntax.Get ("Project");
				yield break;
			}

			if (languageElement.Children == null) {
				yield break;
			}

			foreach (var c in languageElement.Children) {
				if (c.IsAbstract) {
					var abstractChildren = GetAbstractChildren (schemas, languageElement.AbstractChild.SyntaxKind, elementName);
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

		public static IEnumerable<ISymbol> GetAbstractChildren (this IEnumerable<IMSBuildSchema> schemas,
			MSBuildElementSyntax languageElement, string elementName)
		{
			if (languageElement == null) {
				yield return MSBuildElementSyntax.Get ("Project");
				yield break;
			}

			if (languageElement.Children == null) {
				yield break;
			}

			foreach (var c in languageElement.Children) {
				if (c.IsAbstract) {
					var abstractChildren = GetAbstractChildren (schemas, languageElement.AbstractChild.SyntaxKind, elementName);
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

		static IEnumerable<ISymbol> GetAbstractChildren (this IEnumerable<IMSBuildSchema> schemas, MSBuildSyntaxKind kind, string elementName)
		{
			switch (kind) {
			case MSBuildSyntaxKind.Item:
				return schemas.GetItems ();
			case MSBuildSyntaxKind.Task:
				return schemas.GetTasks ();
			case MSBuildSyntaxKind.Property:
				return schemas.GetProperties (false);
			case MSBuildSyntaxKind.Metadata:
				return schemas.GetMetadata (elementName, false);
			}
			return null;
		}

		public static IEnumerable<ISymbol> GetValueCompletions (
			MSBuildValueKind kind,
			MSBuildRootDocument doc,
			MSBuildResolveResult rr = null,
			ExpressionNode triggerExpression = null)
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
				return doc.Frameworks.Select (f => f.Framework).Distinct ().SelectMany (
					id => FrameworkInfoProvider.Instance.GetFrameworkVersions (id)
				).Distinct ().ToList ();
			case MSBuildValueKind.TargetFrameworkProfile:
				return doc.Frameworks.SelectMany (
					tfm => FrameworkInfoProvider.Instance.GetFrameworkProfiles (tfm.Framework, tfm.Version)
				).Distinct ().ToList ();
			case MSBuildValueKind.Configuration:
				return doc.GetConfigurations ().Select (c => new ConstantSymbol(c, "", MSBuildValueKind.Configuration)).ToList ();
			case MSBuildValueKind.Platform:
				return doc.GetPlatforms ().Select (c => new ConstantSymbol (c, "", MSBuildValueKind.Platform)).ToList ();
			case MSBuildValueKind.Condition:
				//FIXME: relax this a bit
				if (triggerExpression != null && triggerExpression is ExpressionText t && t.Length == 0) {
					return Builtins.ConditionFunctions.Values;
				}
				break;
			}

			var fileCompletions = GetFilenameCompletions (kind, doc, triggerExpression, 0, rr);
			if (fileCompletions != null) {
				return fileCompletions;
			}

			return null;
		}

		public static IReadOnlyList<ISymbol> GetFilenameCompletions (
			MSBuildValueKind kind, MSBuildRootDocument doc,
			ExpressionNode triggerExpression, int triggerLength, MSBuildResolveResult rr = null)
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

			string baseDir = null;

			if (rr.AttributeSyntax?.SyntaxKind == MSBuildSyntaxKind.Import_Project && rr.Element != null) {

				var sdkAtt = rr.Element.Attributes.Get ("Sdk", true)?.Value;
				if (string.IsNullOrEmpty (sdkAtt) || !Microsoft.Build.Framework.SdkReference.TryParse (sdkAtt, out var sdkRef)) {
					// if there's an invalid SDK attribute, don't try to provide path completion, it'll be wrong
					return null;
				}

				if (string.Equals(sdkAtt, WorkloadAutoImportPropsLocatorName, StringComparison.OrdinalIgnoreCase)) {
					return new[] { new FileOrFolderInfo ("AutoImport.props", false, "Auto-imported workload properties") };
				}

				var sdkInfo = doc.Environment.ResolveSdk (
					(sdkRef.Name, sdkRef.Version, sdkRef.MinimumVersion), doc.Filename, null);

				// only do path completion for single-path SDKs
				// handling multiple value correctly would involve computing the files that exists in all paths
				// only known case of sdkInfo.Paths with multiple values anyways is WorkloadAutoImportPropsLocator, handled explicitly above
				if (sdkInfo?.Path is string sdkPath && sdkInfo.Paths.Count == 1) {
					baseDir = sdkPath;
				} else {
					return null;
				}
			}

			var basePaths = EvaluateExpressionAsPaths (triggerExpression, doc, triggerLength + 1, baseDir).ToList ();
			return basePaths.Count == 0 ? null : GetPathCompletions (basePaths, includeFiles);
		}

		public static IEnumerable<string> EvaluateExpressionAsPaths (ExpressionNode expression, MSBuildRootDocument doc, int skipEndChars = 0, string baseDir = null)
		{
			baseDir = baseDir ?? Path.GetDirectoryName (doc.Filename);

			if (expression == null) {
				yield return baseDir;
				yield break;
			}

			if (expression is ListExpression list) {
				expression = list.Nodes[list.Nodes.Count - 1];
			}

			if (expression is ExpressionText lit) {
				if (lit.Length == 0) {
					yield return baseDir;
					yield break;
				}
				var path = TrimEndChars (lit.GetUnescapedValue ());
				if (string.IsNullOrEmpty (path)) {
					yield return baseDir;
					yield break;
				}
				//FIXME handle encoding
				if (MSBuildEscaping.FromMSBuildPath (path, baseDir, out var res)) {
					yield return res;
				}
				yield break;
			}

			if (!(expression is ConcatExpression expr && expr.Nodes.All (n=> n is ExpressionText || (n is ExpressionProperty p && p.IsSimpleProperty)))) {
				yield break;
			}

			foreach (var variant in doc.FileEvaluationContext.EvaluatePathWithPermutation (expr, baseDir)) {
				yield return variant;
			}

			string TrimEndChars (string s) => s.Substring (0, Math.Min (s.Length, s.Length - skipEndChars));
		}

		static IReadOnlyList<FileOrFolderInfo> GetPathCompletions (List<string> completionBasePaths, bool includeFiles)
		{
			var infos = new List<FileOrFolderInfo> ();

			foreach (var basePath in completionBasePaths) {
				try {
					if (!Directory.Exists (basePath)) {
						continue;
					}
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
				} catch (Exception ex) {
					LoggingService.LogError ($"Error enumerating paths under '{basePath}'", ex);
				}
			}

			infos.Add (new FileOrFolderInfo ("..", true, "The parent directory"));

			return infos;
		}

		public static ISymbol GetResolvedReference (this MSBuildResolveResult rr, MSBuildRootDocument doc, IFunctionTypeProvider functionTypeProvider)
		{
			switch (rr.ReferenceKind) {
			case MSBuildReferenceKind.Item:
				return doc.GetItem ((string)rr.Reference);
			case MSBuildReferenceKind.Metadata:
				var m = rr.ReferenceAsMetadata;
				return doc.GetMetadata (m.itemName, m.metaName, true);
			case MSBuildReferenceKind.Property:
				return doc.GetProperty ((string)rr.Reference, true);
			case MSBuildReferenceKind.Task:
				return doc.GetTask ((string)rr.Reference);
			case MSBuildReferenceKind.Target:
				return doc.GetTarget ((string)rr.Reference);
			case MSBuildReferenceKind.Keyword:
				return (ISymbol)rr.Reference;
			case MSBuildReferenceKind.KnownValue:
				return (ISymbol)rr.Reference;
			case MSBuildReferenceKind.TargetFramework:
				return ResolveFramework ((string)rr.Reference);
			case MSBuildReferenceKind.TargetFrameworkIdentifier:
				return BestGuessResolveFrameworkIdentifier ((string)rr.Reference, doc.Frameworks);
			case MSBuildReferenceKind.TargetFrameworkVersion:
				return BestGuessResolveFrameworkVersion ((string)rr.Reference, doc.Frameworks);
			case MSBuildReferenceKind.TargetFrameworkProfile:
				return BestGuessResolveFrameworkProfile ((string)rr.Reference, doc.Frameworks);
			case MSBuildReferenceKind.TaskParameter:
				var p = rr.ReferenceAsTaskParameter;
				return doc.GetTaskParameter (p.taskName, p.paramName);
			case MSBuildReferenceKind.ItemFunction:
				//FIXME: attempt overload resolution
				return functionTypeProvider.GetItemFunctionInfo ((string)rr.Reference);
			case MSBuildReferenceKind.StaticPropertyFunction:
				//FIXME: attempt overload resolution
				(string className, string name) = ((string, string))rr.Reference;
				return functionTypeProvider.GetStaticPropertyFunctionInfo (className, name);
			case MSBuildReferenceKind.PropertyFunction:
				//FIXME: attempt overload resolution
				(MSBuildValueKind kind, string funcName) = ((MSBuildValueKind, string))rr.Reference;
				return functionTypeProvider.GetPropertyFunctionInfo (kind, funcName);
			case MSBuildReferenceKind.ClassName:
				return functionTypeProvider.GetClassInfo ((string)rr.Reference);
			case MSBuildReferenceKind.Enum:
				return functionTypeProvider.GetEnumInfo ((string)rr.Reference);
			case MSBuildReferenceKind.ConditionFunction:
				if (Builtins.ConditionFunctions.TryGetValue ((string)rr.Reference, out var conditionFunctionName)) {
					return conditionFunctionName;
				}
				return null;
			}
			return null;
		}

		static FrameworkInfo ResolveFramework (string shortname)
		{
			var fullref = NuGetFramework.ParseFolder (shortname);
			if (fullref.IsSpecificFramework) {
				return new FrameworkInfo (shortname, fullref);
			}
			return null;
		}

		static FrameworkInfo BestGuessResolveFrameworkIdentifier (string identifier, IReadOnlyList<NuGetFramework> docTfms)
		{
			//if any tfm in the doc matches, assume it's referring to that
			var existing = docTfms.FirstOrDefault (d => d.Framework == identifier);
			if (existing != null) {
				return new FrameworkInfo (identifier, existing);
			}
			//else take the latest known version for this framework
			return FrameworkInfoProvider.Instance.GetFrameworkVersions (identifier).LastOrDefault ();
		}

		static FrameworkInfo BestGuessResolveFrameworkVersion (string version, IReadOnlyList<NuGetFramework> docTfms)
		{
			if (!Version.TryParse (version.TrimStart ('v', 'V'), out Version v)) {
				return null;
			}
			//if any tfm in the doc has this version, assume it's referring to that
			var existing = docTfms.FirstOrDefault (d => d.Version == v);
			if (existing != null) {
				return new FrameworkInfo (version, existing);
			}
			//if this matches a known version for any tfm id in the doc, take that
			foreach (var tfm in docTfms) {
				foreach (var f in FrameworkInfoProvider.Instance.GetFrameworkVersions (tfm.Framework)) {
					if (f.Reference.Version == v) {
						return f;
					}
				}
			}
			return null;
		}

		static FrameworkInfo BestGuessResolveFrameworkProfile (string profile, IReadOnlyList<NuGetFramework> docTfms)
		{
			//if any tfm in the doc has this profile, assume it's referring to that
			var existing = docTfms.FirstOrDefault (d => d.Profile == profile);
			if (existing != null) {
				return new FrameworkInfo (profile, existing);
			}
			foreach (var tfm in docTfms) {
				foreach (var f in FrameworkInfoProvider.Instance.GetFrameworkProfiles (tfm.Framework, tfm.Version)) {
					if (string.Equals (f.Name, profile, StringComparison.OrdinalIgnoreCase)) {
						return f;
					}
				}
			}
			return null;
		}

		public static ITypedSymbol GetElementOrAttributeValueInfo (this MSBuildResolveResult rr, IEnumerable<IMSBuildSchema> schemas)
		{
			if (rr.ElementSyntax == null) {
				return null;
			}

			if (rr.AttributeName != null) {
				return schemas.GetAttributeInfo (rr.AttributeSyntax, rr.ElementName, rr.AttributeName);
			}

			return schemas.GetElementInfo (rr.ElementSyntax, rr.ParentName, rr.ElementName);
		}

		public static MSBuildValueKind InferValueKindIfUnknown (this ITypedSymbol variable)
		{
			if (variable.ValueKind != MSBuildValueKind.Unknown) {
				return variable.ValueKind;
			}

			//assume known items are files
			if (variable.ValueKind == MSBuildValueKind.UnknownItem) {
				return MSBuildValueKind.File;
			}

			if (variable.ValueKind == MSBuildValueKind.UnknownItem.AsList ()) {
				return MSBuildValueKind.File.AsList ();
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
					return MSBuildValueKind.TargetName.AsList ();
				}
				if (EndsWith ("Path")) {
					return MSBuildValueKind.FileOrFolder;
				}
				if (EndsWith ("Paths")) {
					return MSBuildValueKind.FileOrFolder.AsList ();
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
					return MSBuildValueKind.Folder.AsList ();
				}
				if (EndsWith ("Files")) {
					return MSBuildValueKind.File.AsList ();
				}
			}

			//make sure these work even if the common targets schema isn't loaded
			if (variable is PropertyInfo prop) {
				switch (prop.Name.ToLowerInvariant ()) {
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
