// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace MonoDevelop.MSBuild.Schema
{
	static class RoslynHelpers
	{
		public static string GetFullName (this ITypeSymbol symbol)
		{
			var sb = new System.Text.StringBuilder ();
			var ns = symbol.ContainingNamespace;
			while (ns != null && !string.IsNullOrEmpty (ns.Name)) {
				sb.Insert (0, '.');
				sb.Insert (0, ns.Name);
				ns = ns.ContainingNamespace;
			}
			sb.Append (symbol.Name);
			return sb.ToString ();
		}
	}
}
