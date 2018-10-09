// Copyright (c) 2014 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using MonoDevelop.MSBuildEditor.Schema;

namespace MonoDevelop.MSBuildEditor.Language
{
	static class FunctionCompletion
	{
		public static IEnumerable<BaseInfo> GetMethodNameCompletions (ExpressionNode triggerExpression)
		{
			if (triggerExpression is Expression expression) {
				triggerExpression = expression.Nodes.Last ();
			}
			var incomplete = (triggerExpression as IncompleteExpressionError)?.IncompleteNode;
			var node = incomplete?.Find (incomplete.Length) as ExpressionPropertyFunctionInvocation;
			if (node == null) {
				return null;
			}

			//string function completion
			if (node.Target is ExpressionPropertyName || node.Target is ExpressionPropertyFunctionInvocation) {
				return GetStringMethods ();
			}

			return null;
		}

		static IEnumerable<BaseInfo> GetStringMethods ()
		{
			var compilation = CreateCoreCompilation ();
			var type = compilation.GetTypeByMetadataName ("System.String");
			foreach (var member in type.GetMembers ()) {
				if (!(member is IMethodSymbol method)) {
					continue;
				}
				if (method.IsStatic || !method.DeclaredAccessibility.HasFlag (Accessibility.Public)) {
					continue;
				}
				yield return new ConstantInfo (method.Name, method.GetDocumentationCommentXml ());
			}
		}

		static Compilation CreateCoreCompilation ()
		{
			return CSharpCompilation.Create (
				"FunctionCompletion",
				references: new[] {
					RoslynHelpers.GetReference (typeof(string).Assembly.Location),
				}
			);
		}
	}
}
