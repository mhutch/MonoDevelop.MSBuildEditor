// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
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
					return value.IsCollapsed
						? value.Value
						: Evaluate (context, ExpressionParser.Parse (value.Value), depth + 1);
				}
				return null;
			}

			case ComplexExpression expr: {
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
							sb.Append (value.IsCollapsed
								? value.Value
								: Evaluate (context, ExpressionParser.Parse (value.Value), depth + 1)
							);
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
				LoggingService.LogWarning ("Only simple properties are supported in imports");
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
			string pathExpression,
			string baseDirectory)
		{
			var expression = ExpressionParser.Parse (pathExpression);

			//fast path for imports without properties, will generally be true for SDK imports
			if (expression is ExpressionText text) {
				yield return context.EvaluatePath (text.Value, baseDirectory);
				yield break;
			}

			//TODO: nested evaluation
			if (expression is ExpressionProperty prop) {
				if (!prop.IsSimpleProperty) {
					LoggingService.LogWarning ("Only simple properties are supported in imports");
					yield break;
				}
				if (context.TryGetProperty (prop.Name, out var value)) {
					if (value.HasMultipleValues) {
						foreach (var v in value.GetValues ()) {
							yield return context.EvaluatePath (v, baseDirectory);
						}
					} else {
						yield return context.EvaluatePath (value.Value, baseDirectory);
					}
				}
				yield break;
			}

			yield return context.EvaluatePath (expression, baseDirectory);
			yield break;

			throw new NotSupportedException ();

			/*
			//ensure each of the properties is fully evaluated
			//FIXME this is super hacky, use real MSBuild evaluation
			foreach (var p in propVals) {
				if (p.Value != null && !readonlyProps.Contains (p.Key)) {
					for (int i = 0; i < p.Value.Count; i++) {
						var val = p.Value[i];
						int recDepth = 0;
						try {
							while (val.IndexOf ('$') > -1 && (recDepth++ < 10)) {
								val = Evaluate (val);
							}
							if (val != null && val.IndexOf ('$') < 0) {
								SetPropertyValue (p.Key, val);
							}
							if (string.IsNullOrEmpty (val)) {
								p.Value.RemoveAt (i);
								i--;
							} else {
								p.Value[i] = val;
							}
						} catch {
							//this happens a lot with things like property functions that
							//index into null values, so make it quiet
							//LoggingService.LogDebug ($"Error evaluating property {p.Key}={val}");
							//FIXME stop ignoring these errors
						}
					}
				}
			}

			//permute on properties for which we have multiple values
			var expr = ExpressionParser.Parse (path, ExpressionOptions.None);
			var propsToPermute = new List<(string, List<string>)> ();
			foreach (var prop in expr.WithAllDescendants ().OfType<ExpressionProperty> ()) {
				if (readonlyProps.Contains (prop.Name)) {
					continue;
				}
				if (propVals != null && propVals.TryGetValues (prop.Name, out List<string> values) && values != null) {
					if (values.Count > 1) {
						propsToPermute.Add ((prop.Name, values));
					}
				} else if (extensionPaths != null && string.Equals (prop.Name, "MSBuildExtensionsPath", StringComparison.OrdinalIgnoreCase) || string.Equals (prop.Name, "MSBuildExtensionsPath32", StringComparison.OrdinalIgnoreCase)) {
					propsToPermute.Add ((prop.Name, extensionPaths));
				}
			}

			if (propsToPermute.Count == 0) {
				yield return EvaluatePath (path, basePath);
			} else {
				foreach (var ctx in PermuteProperties (this, propsToPermute)) {
					yield return EvaluatePath (path, basePath);
				}
		}

		//TODO: guard against excessive permutation
		//TODO: return a new context instead of altering this one?
		static IEnumerable<IMSBuildEvaluationContext> PermuteProperties (IMSBuildEvaluationContext evalCtx, List<(string, List<string>)> multivaluedProperties, int idx = 0)
		{
			var prop = multivaluedProperties[idx];
			var name = prop.Item1;
			// the list may contain multiple of the same item
			// we don't just convert it into a hashset as it needs to preserve order
			var seen = new HashSet<string> ();
			foreach (var val in prop.Item2) {
				if (!seen.Add (val) || string.IsNullOrEmpty (val)) {
					continue;
				}
				evalCtx.SetPropertyValue (name, val);
				if (idx + 1 == multivaluedProperties.Count) {
					yield return evalCtx;
				} else {
					foreach (var permutation in PermuteProperties (evalCtx, multivaluedProperties, idx + 1)) {
						yield return permutation;
					}
				}
			}
		}
			}*/
		}
	}
}