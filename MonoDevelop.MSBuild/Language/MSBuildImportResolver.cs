// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using MonoDevelop.MSBuild.Evaluation;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.SdkResolution;

namespace MonoDevelop.MSBuild.Language
{
	class MSBuildImportResolver
	{
		IMSBuildEvaluationContext fileEvalContext;
		readonly MSBuildParserContext parseContext;
		readonly string parentFilePath;

		public MSBuildImportResolver (MSBuildParserContext parseContext, string parentFilePath)
			: this (parseContext, parentFilePath, null)
		{
		}

		public MSBuildImportResolver (MSBuildParserContext parseContext, string parentFilePath, IMSBuildEvaluationContext fileEvalContext)
		{
			this.parseContext = parseContext;
			this.parentFilePath = parentFilePath;
			this.fileEvalContext = fileEvalContext;
		}

		public IEnumerable<Import> Resolve (ExpressionNode importExpr, string importExprString, string sdkString, SdkInfo resolvedSdk)
		{
			fileEvalContext = fileEvalContext
				?? new MSBuildFileEvaluationContext (
					parseContext.RuntimeEvaluationContext,
					parseContext.ProjectPath, parentFilePath);

			return parseContext.ResolveImport (
				fileEvalContext,
				parentFilePath,
				importExpr,
				importExprString,
				sdkString,
				resolvedSdk);
		}
	}
}
