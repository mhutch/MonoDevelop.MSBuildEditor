// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP
#nullable enable
#endif

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using MonoDevelop.MSBuild.Analysis;
using MonoDevelop.MSBuild.Dom;
using MonoDevelop.MSBuild.Evaluation;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.SdkResolution;

namespace MonoDevelop.MSBuild.Language;

class MSBuildImportResolver
{
	readonly MSBuildParserContext parseContext;
	readonly string? parentFilePath;
	IMSBuildEvaluationContext? fileEvalContext;
	MSBuildCollectedValuesEvaluationContext? evalContext;

	public MSBuildImportResolver (MSBuildParserContext parseContext, string? parentFilePath)
		: this (parseContext, parentFilePath, null)
	{
	}

	public MSBuildImportResolver (MSBuildParserContext parseContext, string? parentFilePath, IMSBuildEvaluationContext? fileEvalContext)
	{
		this.parseContext = parseContext;
		this.parentFilePath = parentFilePath;
		this.fileEvalContext = fileEvalContext;
	}

	public IMSBuildEvaluationContext FileEvaluationContext
		=> fileEvalContext ??= MSBuildFileEvaluationContext.Create (parseContext.ProjectEvaluationContext, parseContext.Logger, parentFilePath);

	IMSBuildEvaluationContext EvaluationContext => evalContext ??= new MSBuildCollectedValuesEvaluationContext (FileEvaluationContext, parseContext.PropertyCollector);

	public IEnumerable<Import> Resolve (ExpressionNode importExpr, string importExprString, string? sdkString, SdkInfo? resolvedSdk, bool isImplicitImport = false)
	{
		//yield a placeholder for tooltips, imports pad etc to query
		if (sdkString is not null && resolvedSdk is null) {
			yield return new Import (importExprString, sdkString, null, resolvedSdk, DateTime.MinValue, false);
			yield break;
		}

		//FIXME: add support for MSBuildUserExtensionsPath, the context does not currently support it
		if (importExprString.IndexOf ("$(MSBuildUserExtensionsPath)", StringComparison.OrdinalIgnoreCase) > -1) {
			yield break;
		}

		bool foundAny = false;
		bool isWildcard = false;

		IList<string?> basePaths;
		if (resolvedSdk != null) {
			basePaths = resolvedSdk.Paths!;
		} else {
			basePaths = [ Path.GetDirectoryName (parentFilePath) ];
		}

		foreach (var filename in basePaths.SelectMany (basePath => EvaluationContext.EvaluatePathWithPermutation (importExpr, basePath))) {
			if (string.IsNullOrEmpty (filename)) {
				continue;
			}

			//dedup
			if (!parseContext.ImportedFiles.Add (filename)) {
				foundAny = true;
				continue;
			}

			//wildcards
			var wildcardIdx = filename.IndexOf ('*');

			//arbitrary limit to skip improbably short values from bad evaluation
			const int MIN_WILDCARD_STAR_IDX = 15;
			const int MIN_WILDCARD_PATTERN_IDX = 10;
			if (wildcardIdx > MIN_WILDCARD_STAR_IDX) {
				isWildcard = true;
				var lastSlash = filename.LastIndexOf (Path.DirectorySeparatorChar);
				if (lastSlash < MIN_WILDCARD_PATTERN_IDX) {
					continue;
				}
				if (lastSlash > wildcardIdx) {
					continue;
				}

				string[] files;
				try {
					var dir = filename.Substring (0, lastSlash);
					if (!Directory.Exists (dir)) {
						continue;
					}

					//finding the folder's enough for this to "count" as resolved even if there aren't any files in it
					foundAny = true;

					var pattern = filename.Substring (lastSlash + 1);

					files = Directory.GetFiles (dir, pattern);
				} catch (Exception ex) when (parseContext.IsNotCancellation (ex)) {
					parseContext.LogErrorEvaluatingImportWildcardCandidate (ex, filename);
					continue;
				}

				foreach (var f in files) {
					Import wildImport;
					try {
						wildImport = parseContext.GetCachedOrParse (importExprString, f, sdkString, resolvedSdk, File.GetLastWriteTimeUtc (f));
					} catch (Exception ex) when (parseContext.IsNotCancellation (ex)) {
						parseContext.LogErrorReadingImportWildcardCandidate (ex, f);
						continue;
					}
					yield return wildImport;
				}

				continue;
			}

			Import import;
			try {
				var fi = new FileInfo (filename);
				if (!fi.Exists) {
					continue;
				}
				import = parseContext.GetCachedOrParse (importExprString, filename, sdkString, resolvedSdk, fi.LastWriteTimeUtc, isImplicitImport);
			} catch (Exception ex) when (parseContext.IsNotCancellation (ex)) {
				parseContext.LogErrorReadingImportCandidate (ex, filename);
				continue;
			}

			foundAny = true;
			yield return import;
			continue;
		}

		//yield a placeholder for tooltips, imports pad etc to query
		if (!foundAny) {
			yield return new Import (importExprString, sdkString, null, resolvedSdk, DateTime.MinValue, false);
		}

		// we skip logging for wildcards as these are generally extensibility points that are often unused
		// this is here (rather than being folded into the next condition) for ease of breakpointing
		if (!foundAny && !isWildcard) {
			parseContext.LogCouldNotResolveImport (importExprString);
		}
	}

	public (SdkInfo sdk, string sdkReference)? ResolveSdk<TElement> (MSBuildDocument doc, TElement element) where TElement : MSBuildElement, IElementHasSdkReference
	{
		if (element.SdkAttribute is not MSBuildAttribute nameAttribute) {
			throw new ArgumentException ($"{nameof (element)}.{nameof (element.SdkAttribute)} cannot be null");
		}

		string? sdkName = null;
		if (nameAttribute.Value is ExpressionText nameText) {
			sdkName = nameText.GetUnescapedValue (false, out _, out _);
		} else if (nameAttribute.Value is not null) {
			if (CheckHasItemsOrMetadata (doc, nameAttribute)) {
				return null;
			}
			sdkName = EvaluationContext.Evaluate (nameAttribute.Value).Unescape ();
		}

		if (string.IsNullOrEmpty (sdkName)) {
			if (doc.IsTopLevel) {
				doc.Diagnostics.Add (CoreDiagnostics.EmptySdkName, nameAttribute.Value?.Span ?? nameAttribute.XAttribute.NameSpan);
			}
			return null;
		}

		var valueSpan = nameAttribute.Value!.Span; // Value is not null when sdkName is not null

		string? sdkVersion = null;
		if (element.VersionAttribute is { } versionAttribute) {
			if (versionAttribute.Value is ExpressionText versionText) {
				sdkVersion = versionText.GetUnescapedValue (false, out _, out _);
			} else if (versionAttribute.Value is not null && !CheckHasItemsOrMetadata (doc, versionAttribute)) {
				sdkVersion = EvaluationContext.Evaluate (versionAttribute.Value).Unescape ();
			} else {
				return null;
			}
		}

		string? sdkMinimumVersion = null;
		if (element.MinimumVersionAttribute is { } minVersionAttribute) {
			if (minVersionAttribute.Value is ExpressionText minVersionText) {
				sdkMinimumVersion = minVersionText.GetUnescapedValue (false, out _, out _);
			} else if (minVersionAttribute.Value is not null && !CheckHasItemsOrMetadata (doc, minVersionAttribute)) {
				sdkMinimumVersion = EvaluationContext.Evaluate (minVersionAttribute.Value).Unescape ();
			} else {
				return null;
			}
		}

		var sdkRef = new MSBuildSdkReference (sdkName, sdkVersion, sdkMinimumVersion);

		var sdkInfo = parseContext.ResolveSdk (doc, sdkRef, valueSpan);
		if (sdkInfo is null) {
			return null;
		}

		if (doc.IsTopLevel) {
			foreach (var p in sdkInfo.Paths) {
				doc.Annotations.Add (element.SdkAttribute.XAttribute, new NavigationAnnotation (p,nameAttribute.Value.Span));
			}
		}

		return (sdkInfo, sdkRef.ToString ());
	}

	static bool CheckHasItemsOrMetadata (MSBuildDocument doc, MSBuildAttribute attribute)
	{
		if (attribute.Value is null) {
			return false;
		}

		ExpressionNode? forbiddenNode = null;
		foreach (var n in attribute.Value.WithAllDescendants ()) {
			switch (n) {
			case ExpressionItem:
			case ExpressionMetadata:
				forbiddenNode = n;
				break;
			}
		}

		if (forbiddenNode is not null && doc.IsTopLevel) {
			doc.Diagnostics.Add (CoreDiagnostics.AttributeOnlyPermitsProperties, forbiddenNode.Span, attribute.Name);
			return true;
		}

		return false;
	}
}
