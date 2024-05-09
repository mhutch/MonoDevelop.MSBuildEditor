// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using MonoDevelop.MSBuild.Analysis;
using MonoDevelop.MSBuild.Dom;
using MonoDevelop.MSBuild.Evaluation;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.SdkResolution;

namespace MonoDevelop.MSBuild.Language;

class MSBuildImportResolver
{
	IMSBuildEvaluationContext fileEvalContext;
	readonly MSBuildParserContext parseContext;
	readonly string parentFilePath;
	readonly MSBuildCollectedValuesEvaluationContext evalCtx;

	public MSBuildImportResolver (MSBuildParserContext parseContext, string parentFilePath)
		: this (parseContext, parentFilePath, null)
	{
	}

	public MSBuildImportResolver (MSBuildParserContext parseContext, string parentFilePath, IMSBuildEvaluationContext fileEvalContext)
	{
		this.parseContext = parseContext;
		this.parentFilePath = parentFilePath;
		this.fileEvalContext = fileEvalContext;
		this.evalCtx = new MSBuildCollectedValuesEvaluationContext (FileEvaluationContext, parseContext.PropertyCollector);
	}

	public IEnumerable<Import> Resolve (ExpressionNode importExpr, string importExprString, string sdkString, SdkInfo resolvedSdk, bool isImplicitImport = false)
		=> parseContext.ResolveImport (
			FileEvaluationContext,
			parentFilePath,
			importExpr,
			importExprString,
			sdkString,
			resolvedSdk,
			isImplicitImport);

	public IMSBuildEvaluationContext FileEvaluationContext
		=> fileEvalContext ??=  MSBuildFileEvaluationContext.Create (parseContext.ProjectEvaluationContext, parseContext.Logger, parentFilePath);

	public SdkInfo ResolveSdk<TElement> (MSBuildDocument doc, TElement element) where TElement : MSBuildElement, IElementHasSdkReference
	{
		if (element.SdkAttribute is not MSBuildAttribute nameAttribute) {
			throw new ArgumentException ($"{nameof (element)}.{nameof (element.SdkAttribute)} cannot be null");
		}

		string sdkName = null;
		if (nameAttribute.Value is ExpressionText nameText) {
			sdkName = nameText.GetUnescapedValue (false, out _, out _);
		} else if (nameAttribute.Value is not null) {
			if (!CheckOnlyPropertiesInExpression (doc, nameAttribute)) {
				return null;
			}
			sdkName = evalCtx.Evaluate (nameAttribute.Value).Unescape ();
		}

		if (string.IsNullOrEmpty (sdkName)) {
			if (doc.IsToplevel) {
				doc.Diagnostics.Add (CoreDiagnostics.EmptySdkName, nameAttribute.Value?.Span ?? nameAttribute.XAttribute.NameSpan);
			}
			return null;
		}

		string sdkVersion = null;
		if (element.VersionAttribute is { } versionAttribute) {
			if (versionAttribute.Value is ExpressionText versionText) {
				sdkVersion = versionText.GetUnescapedValue (false, out _, out _);
			} else if (versionAttribute.Value is not null && CheckOnlyPropertiesInExpression (doc, versionAttribute)) {
				sdkVersion = evalCtx.Evaluate (versionAttribute.Value).Unescape ();
			} else {
				return null;
			}
		}

		string sdkMinimumVersion = null;
		if (element.MinimumVersionAttribute is { } minVersionAttribute) {
			if (minVersionAttribute.Value is ExpressionText minVersionText) {
				sdkMinimumVersion = minVersionText.GetUnescapedValue (false, out _, out _);
			} else if (minVersionAttribute.Value is not null && CheckOnlyPropertiesInExpression (doc, minVersionAttribute)) {
				sdkMinimumVersion = evalCtx.Evaluate (minVersionAttribute.Value).Unescape ();
			} else {
				return null;
			}
		}

		var sdk = new MSBuildSdkReference (sdkName, sdkVersion, sdkMinimumVersion);

		var sdkInfo = parseContext.ResolveSdk (doc, sdk, nameAttribute.Value.Span);
		if (sdkInfo is null) {
			return null;
		}

		if (doc.IsToplevel) {
			foreach (var p in sdkInfo.Paths) {
				doc.Annotations.Add (element.SdkAttribute.XAttribute, new NavigationAnnotation (p,nameAttribute.Value.Span));
			}
		}

		return sdkInfo;
	}

	static bool CheckOnlyPropertiesInExpression (MSBuildDocument doc, MSBuildAttribute attribute)
	{
		ExpressionNode? forbiddenNode = null;
		foreach (var n in attribute.Value.WithAllDescendants ()) {
			switch (n) {
			case ExpressionItem:
			case ExpressionMetadata:
				forbiddenNode = n;
				break;
			}
		}

		if (forbiddenNode is not null) {
			doc.Diagnostics.Add (CoreDiagnostics.AttributeOnlyPermitsProperties, forbiddenNode.Span, attribute.Name);
			return false;
		}

		return true;
	}
}
