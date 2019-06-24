// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.CodeAnalysis;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo ("MonoDevelop.MSBuild.Tests")]

namespace MonoDevelop.MSBuild
{
	static class Ambience
	{
		public static string GetSummaryMarkup (IMethodSymbol symbol) => "";
		public static string GetSummaryMarkup (IParameterSymbol symbol) => "";
		public static string GetSummaryMarkup (ITypeSymbol symbol) => "";
		public static string GetSummaryMarkup (IPropertySymbol symbol) => "";
	}
}