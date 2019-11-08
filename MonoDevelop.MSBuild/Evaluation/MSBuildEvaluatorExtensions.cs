// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Util;

namespace MonoDevelop.MSBuild.Evaluation
{
	static class MSBuildEvaluatorExtensions
	{
		const int maxEvaluationDepth = 50;

		public static string Evaluate (this IMSBuildEvaluationContext context, string expression)
			=> Evaluate (context, ExpressionParser.Parse (expression));

		public static string Evaluate (this IMSBuildEvaluationContext context, ExpressionNode expression) => Evaluate (context, expression, 0);

		static string Evaluate (this IMSBuildEvaluationContext context, ExpressionNode expression, int depth)
		{
			if (depth == maxEvaluationDepth) {
				throw new Exception ("Property evaluation exceeded maximum depth");
			}

			switch (expression) {

			case ExpressionText text:
				return text.Value;

			case ExpressionProperty prop: {
				if (!prop.IsSimpleProperty) {
					LoggingService.LogWarning ("Only simple properties are supported in imports");
					return null;
				}
				if (context.TryGetProperty (prop.Name, out var value)) {
					return Evaluate (context, value.Value, depth + 1);
				}
				return null;
			}

			case ConcatExpression expr: {
				var sb = new StringBuilder ();
				foreach (var n in expr.Nodes) {
					switch (n) {
					case ExpressionText t:
						sb.Append (t.Value);
						continue;
					case ExpressionProperty p:
						if (!p.IsSimpleProperty) {
							LoggingService.LogWarning ("Only simple properties are supported in imports");
							return null;
						}
						if (context.TryGetProperty (p.Name, out var value)) {
							sb.Append (Evaluate (context, value.Value, depth + 1));
						}
						continue;
					default:
						LoggingService.LogWarning ("Only simple properties are supported in imports");
						return null;
					}
				}
				return sb.ToString ();
			}

			default:
				LoggingService.LogWarning ("Only simple properties and expressions are supported in imports");
				return null;
			}
		}

		public static string EvaluatePath (
			this IMSBuildEvaluationContext context,
			ExpressionNode expression,
			string baseDirectory)
			=> MSBuildEscaping.FromMSBuildPath (context.Evaluate(expression), baseDirectory);

		public static string EvaluatePath (
			this IMSBuildEvaluationContext context,
			string expression,
			string baseDirectory)
			=> context.EvaluatePath (ExpressionParser.Parse (expression), baseDirectory);

		// FIXME: need to make this more efficient.
		// can we ignore results where a property was simply not found?
		// can we tokenize it and check each level of the path exists before drilling down?
		// can we cache the filesystem lookups?
		public static IEnumerable<string> EvaluatePathWithPermutation (
			this IMSBuildEvaluationContext context,
			ExpressionNode pathExpression,
			string baseDirectory)
		{
			foreach (var p in EvaluateWithPermutation (context, null, pathExpression, 0)) {
				if (p == null) {
					continue;
				}
				yield return MSBuildEscaping.FromMSBuildPath (p, baseDirectory);
			}
		}

		public static IEnumerable<string> EvaluateWithPermutation (this IMSBuildEvaluationContext context, string expression)
			=> EvaluateWithPermutation (context, null, ExpressionParser.Parse (expression), 0);

		public static IEnumerable<string> EvaluateWithPermutation (this IMSBuildEvaluationContext context, ExpressionNode expression)
			=> EvaluateWithPermutation (context, null, expression, 0);

		static IEnumerable<string> EvaluateWithPermutation (this IMSBuildEvaluationContext context, string prefix, ExpressionNode expression, int depth)
		{
			switch (expression) {
			// yield plain text
			case ExpressionText text:
				yield return prefix + text.Value;
				yield break;

			// recursively yield evaluated property
			case ExpressionProperty prop: {
				if (!prop.IsSimpleProperty) {
					LoggingService.LogWarning ("Only simple properties are supported in imports");
					break;
				}
				if (context.TryGetProperty (prop.Name, out var value)) {
					if (value.HasMultipleValues) {
						if (value.IsCollapsed) {
							foreach (var v in value.GetValues ()) {
								yield return prefix + ((ExpressionText)v).Value;
							}
						} else {
							foreach (var v in value.GetValues ()) {
								foreach (var evaluated in EvaluateWithPermutation (context, prefix, v, depth + 1)) {
									yield return evaluated;
								}
							}
						}
					} else {
						if (value.IsCollapsed) {
							yield return prefix + ((ExpressionText)value.Value).Value;
						} else {
							foreach (var evaluated in EvaluateWithPermutation (context, prefix, value.Value, depth + 1)) {
								yield return evaluated;
							}
						}
					}
					break;
				} else {
					yield return prefix;
				}
				yield break;
			}

			case ConcatExpression expr: {

				var nodes = expr.Nodes;

				if (nodes.Count == 0) {
					yield break;
				}

				if (nodes.Count == 1) {
					foreach (var evaluated in EvaluateWithPermutation (context, prefix, nodes[0], depth + 1)) {
						yield return evaluated;
					}
					yield break;
				}

				var zero = nodes[0];
				var skip = new ExpressionNode[nodes.Count - 1];
				for (int i = 1; i < nodes.Count; i++) {
					skip[i - 1] = nodes[i];
				}

				foreach (var zeroVal in EvaluateWithPermutation (context, prefix, zero, depth + 1)) {
					ExpressionNode inner = skip.Length == 1 ? skip[0] : new ConcatExpression (0, 0, skip);
					foreach (var v in EvaluateWithPermutation (context, zeroVal, inner, depth + 1)) {
						yield return v;
					}
				}
				yield break;
			}

			default:
				LoggingService.LogWarning ("Only simple properties and expressions are supported in imports");
				yield break;
			}
		}
	}
}