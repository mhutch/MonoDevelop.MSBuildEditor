// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace MonoDevelop.MSBuild.Editor.Roslyn
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

		// loading the docs from roslyn can be expensive, return an empty string and the symbol
		// this mean callers have to resolve the docs from the symbol themselves. it's a lot
		// simpler to push the async logic to the callers than to make all the BaseInfo.Description
		// implementations and usages async
		public static DisplayText GetDescription (ISymbol symbol) => new DisplayText ("", symbol);
	}
}
