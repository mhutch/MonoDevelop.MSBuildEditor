// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using MonoDevelop.MSBuild.Language;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo ("MonoDevelop.MSBuild.Tests")]

namespace MonoDevelop.MSBuild
{
	static class Ambience
	{
		public static string GetSummaryMarkup (IMethodSymbol symbol) => throw new NotImplementedException ();
		public static string GetSummaryMarkup (IParameterSymbol symbol) => throw new NotImplementedException ();
		public static string GetSummaryMarkup (ITypeSymbol symbol) => throw new NotImplementedException ();
		public static string GetSummaryMarkup (IPropertySymbol symbol) => throw new NotImplementedException ();
	}

	static class Extensions
	{
		public static ITypeSymbol GetReturnType (this IPropertySymbol property) => throw new NotImplementedException ();
		public static ITypeSymbol GetReturnType (this IMethodSymbol method) => throw new NotImplementedException ();
	}

	static class MSBuildEditorHost
	{
		public static Compilation GetMSBuildCompilation () => throw new NotImplementedException ();
	}

	interface IMSBuildEvaluationContext
	{
		IEnumerable<string> EvaluatePathWithPermutation (string pathExpression, string baseDirectory, PropertyValueCollector propVals);
	}
}