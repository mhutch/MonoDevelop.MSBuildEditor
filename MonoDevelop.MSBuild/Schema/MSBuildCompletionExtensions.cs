// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Extensions.Logging;

using MonoDevelop.MSBuild.Evaluation;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Language.Syntax;
using MonoDevelop.MSBuild.Language.Typesystem;
using MonoDevelop.MSBuild.Util;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Logging;

using NuGet.Frameworks;

namespace MonoDevelop.MSBuild.Schema
{
	static partial class MSBuildCompletionExtensions
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
							if (spat.SyntaxKind == MSBuildSyntaxKind.Item_Update) {
								continue;
							}
						} else {
							switch (spat.SyntaxKind) {
							case MSBuildSyntaxKind.Item_KeepMetadata:
							case MSBuildSyntaxKind.Item_RemoveMetadata:
							case MSBuildSyntaxKind.Item_KeepDuplicates:
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
				if (element.ParentElement is XElement te && te.Name.Equals (MSBuildElementSyntax.Target.Name, true)) {
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
				yield return MSBuildElementSyntax.Project;
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
				yield return MSBuildElementSyntax.Project;
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

		/// <summary>
		/// Gets completions for the value of the given symbol, based on its value kind and complex type (if any).
		/// </summary>
		/// <param name="kindIfUnknown">
		/// Optionally provide an alternate <see cref="MSBuildValueKind"/> to be used if the the <see cref="ITypedSymbol"/>'s value kind is <see cref="MSBuildValueKind.Unknown"/>.
		/// </param>
		public static IEnumerable<ISymbol> GetValueCompletions (
			ITypedSymbol symbol,
			MSBuildRootDocument doc,
			IMSBuildFileSystem fileSystem,
			ILogger logger,
			MSBuildResolveResult rr = null,
			ExpressionNode triggerExpression = null,
			MSBuildValueKind kindIfUnknown = MSBuildValueKind.Unknown)
		{
			var kind = symbol.ValueKindWithoutModifiers ();

			// FIXME: This is a temporary hack so we have completion for imported XSD schemas with missing type info.
			// It is not needed for inferred schemas, as they have already performed the inference.
			if (kind == MSBuildValueKind.Unknown) {
				kind = kindIfUnknown;
			}

			switch (kind) {
			case MSBuildValueKind.TaskOutputParameterName:
				return doc.GetTaskParameters (rr.ParentName).Where (p => p.IsOutput).ToList ();
			case MSBuildValueKind.TargetName:
				return doc.GetTargets ().ToList ();
			case MSBuildValueKind.PropertyName:
				bool includeReadOnly = rr.AttributeSyntax?.SyntaxKind != MSBuildSyntaxKind.Output_PropertyName;
				return doc.GetProperties (includeReadOnly).ToList ();
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
					return MSBuildIntrinsics.ConditionFunctions.Values;
				}
				break;
			}

			if (doc.GetSchemas (false).TryGetKnownValues (symbol, out var knownValues, kindIfUnknown: kind)) {
				return knownValues;
			}

			var fileCompletions = fileSystem.GetFilenameCompletions (kind, doc, triggerExpression, 0, logger, rr);
			if (fileCompletions != null) {
				return fileCompletions;
			}

			return null;
		}

		public static IReadOnlyList<ISymbol> GetFilenameCompletions (
			this IMSBuildFileSystem fileSystem,
			MSBuildValueKind kind, MSBuildRootDocument doc,
			ExpressionNode triggerExpression, int triggerLength, ILogger logger, MSBuildResolveResult rr = null)
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

			if (rr.AttributeSyntax?.SyntaxKind == MSBuildSyntaxKind.Import_Project && rr.ElementSymbol != null) {

				var sdkAtt = rr.Element.Attributes.Get (MSBuildAttributeName.Sdk, true)?.Value;
				if (string.IsNullOrEmpty (sdkAtt) || !MSBuildSdkReference.TryParse (sdkAtt, out var sdkRef)) {
					// if there's an invalid SDK attribute, don't try to provide path completion, it'll be wrong
					return null;
				}

				if (string.Equals(sdkAtt, WorkloadAutoImportPropsLocatorName, StringComparison.OrdinalIgnoreCase)) {
					return new[] { new FileOrFolderInfo ("AutoImport.props", false, "Auto-imported workload properties") };
				}

				var sdkInfo = doc.Environment.ResolveSdk (sdkRef, doc.Filename, null, doc.FileEvaluationContext.Logger);

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
			return basePaths.Count == 0 ? null : fileSystem.GetPathCompletions (basePaths, includeFiles, logger);
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
				var path = TrimEndChars (lit.GetUnescapedValue (false, out _, out _));
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

		static IReadOnlyList<FileOrFolderInfo> GetPathCompletions (this IMSBuildFileSystem fileSystem, List<string> completionBasePaths, bool includeFiles, ILogger logger)
		{
			var infos = new List<FileOrFolderInfo> ();

			foreach (var basePath in completionBasePaths) {
				try {
					if (!fileSystem.DirectoryExists (basePath)) {
						continue;
					}
					foreach (var e in fileSystem.GetDirectories (basePath)) {
						var name = Path.GetFileName (e);
						infos.Add (new FileOrFolderInfo (name, true, e));
					}

					if (includeFiles) {
						foreach (var e in fileSystem.GetFiles (basePath)) {
							var name = Path.GetFileName (e);
							infos.Add (new FileOrFolderInfo (name, false, e));
						}
					}
				} catch (Exception ex) {
					LogFailedToEnumeratePaths (logger, basePath, ex);
				}
			}

			infos.Add (new FileOrFolderInfo ("..", true, "The parent directory"));

			return infos;
		}

		public static ISymbol GetResolvedReference (this MSBuildResolveResult rr, MSBuildRootDocument doc, IFunctionTypeProvider functionTypeProvider)
		{
			static bool AreEqual (string? a, string? b) => string.Equals (a, b, StringComparison.OrdinalIgnoreCase);

			switch (rr.ReferenceKind) {
			case MSBuildReferenceKind.Item:
				// it's it's an item element, it's already captured on the resolver
				string itemName = rr.GetItemReference ();
				if (rr.ElementSymbol is ItemInfo item && AreEqual (item.Name, itemName)) {
					return rr.ElementSymbol;
				}
				// if it's an item reference within an expression, we need to resolve it
				return  doc.GetItem (rr.GetItemReference ());
			case MSBuildReferenceKind.Metadata:
				var m = rr.GetMetadataReference ();
				bool IsMetadataMatch (MetadataInfo possibleMetadata) => AreEqual (possibleMetadata.Name, m.metaName) && AreEqual (possibleMetadata.Item?.Name, m.itemName);
				if (rr.AttributeSymbol is MetadataInfo metadataAttribute && IsMetadataMatch (metadataAttribute)) {
					return metadataAttribute;
				}
				if (rr.AttributeSymbol is MetadataInfo metadataElement && IsMetadataMatch (metadataElement)) {
					return metadataElement;
				}
				return doc.GetMetadata (m.itemName, m.metaName, true);
			case MSBuildReferenceKind.Property:
				string propertyName = rr.GetPropertyReference ();
				if (rr.ElementSymbol is PropertyInfo propertySymbol && AreEqual (propertySymbol.Name, propertyName)) {
					return propertySymbol;
				}
				return doc.GetProperty (propertyName, true);
			case MSBuildReferenceKind.Task:
				string taskName = rr.GetTaskReference ();
				if (rr.ElementSymbol is TaskInfo taskSymbol && AreEqual (taskSymbol.Name, taskName)) {
					return taskSymbol;
				}
				return doc.GetTask (taskName);
			case MSBuildReferenceKind.Target:
				string targetName = rr.GetTargetReference ();
				if (rr.ElementSymbol is TargetInfo targetSymbol && AreEqual (targetSymbol.Name, targetName)) {
					return targetSymbol;
				}
				return doc.GetTarget (targetName);
			case MSBuildReferenceKind.Keyword:
				return rr.GetKeywordReference ();
			case MSBuildReferenceKind.KnownValue:
				return rr.GetKnownValueReference ();
			case MSBuildReferenceKind.TargetFramework:
				return FrameworkInfoProvider.TryGetFrameworkInfo (rr.GetTargetFrameworkReference ());
			case MSBuildReferenceKind.TargetFrameworkIdentifier:
				return BestGuessResolveFrameworkIdentifier (rr.GetTargetFrameworkIdentifierReference (), doc.Frameworks);
			case MSBuildReferenceKind.TargetFrameworkVersion:
				return BestGuessResolveFrameworkVersion (rr.GetTargetFrameworkVersionReference(), doc.Frameworks);
			case MSBuildReferenceKind.TargetFrameworkProfile:
				return BestGuessResolveFrameworkProfile (rr.GetTargetFrameworkProfileReference(), doc.Frameworks);
			case MSBuildReferenceKind.TaskParameter:
				var p = rr.GetTaskParameterReference ();
				if (rr.AttributeSymbol is TaskParameterInfo parameterSymbol && AreEqual (parameterSymbol.Name, p.paramName)) {
					return parameterSymbol;
				}
				return doc.GetTaskParameter (p.taskName, p.paramName);
			case MSBuildReferenceKind.ItemFunction:
				//FIXME: attempt overload resolution
				return functionTypeProvider.GetItemFunctionInfo (rr.GetItemFunctionReference ());
			case MSBuildReferenceKind.StaticPropertyFunction:
				//FIXME: attempt overload resolution
				(string className, string name) = rr.GetStaticPropertyFunctionReference ();
				return functionTypeProvider.GetStaticPropertyFunctionInfo (className, name);
			case MSBuildReferenceKind.PropertyFunction:
				//FIXME: attempt overload resolution
				(MSBuildValueKind kind, string funcName) = rr.GetPropertyFunctionReference ();
				return functionTypeProvider.GetPropertyFunctionInfo (kind, funcName);
			case MSBuildReferenceKind.ClassName:
				return functionTypeProvider.GetClassInfo (rr.GetClassNameReference ());
			case MSBuildReferenceKind.Enum:
				return functionTypeProvider.GetEnumInfo (rr.GetEnumReference ());
			case MSBuildReferenceKind.ConditionFunction:
				if (MSBuildIntrinsics.ConditionFunctions.TryGetValue (rr.GetConditionFunctionReference (), out var conditionFunctionName)) {
					return conditionFunctionName;
				}
				return null;
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

		[LoggerMessage (EventId = 0, Level = LogLevel.Error, Message = "Failed to enumerate children for file path {filePath}")]
		static partial void LogFailedToEnumeratePaths (ILogger logger, UserIdentifiableFileName filePath, Exception ex);
	}
}
