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
		public static EvaluatedValue Evaluate (this IMSBuildEvaluationContext context, string expression)
			=> Evaluate (context, ExpressionParser.Parse (expression));

		public static EvaluatedValue Evaluate (this IMSBuildEvaluationContext context, ExpressionNode expression) => new (EvaluateNodeAsString (context, expression));

		static string EvaluateNodeAsString (this IMSBuildEvaluationContext context, ExpressionNode expression) => Stringify (EvaluateNode (context, expression));

		static object EvaluateNode (this IMSBuildEvaluationContext context, ExpressionNode expression)
		{
			switch (expression) {

			case ExpressionText text:
				return text.Value;

			case ExpressionProperty prop:
				return EvaluateProperty (context, prop);

			case ConcatExpression expr:
				return EvaluateConcat (context, expr);

			case QuotedExpression quotedExpr:
				// TODO: unescaping
				return EvaluateNodeAsString (context, quotedExpr.Expression) ?? "";

			default:
				LoggingService.LogWarning ("Only simple properties and expressions are supported in imports");
				return null;
			}
		}

		static string EvaluateConcat (IMSBuildEvaluationContext context, ConcatExpression expr)
		{
			var sb = new StringBuilder ();
			foreach (var n in expr.Nodes) {
				string evaluated = EvaluateNodeAsString (context, n);
				sb.Append (evaluated);
			}
			return sb.ToString ();
		}

		static object EvaluateProperty (IMSBuildEvaluationContext context, ExpressionProperty prop)
		{
			if (prop.Expression is ExpressionPropertyFunctionInvocation inv && inv.Target is ExpressionClassReference classRef) {
				bool propEvalSuccess = false;
				object propEvalResult = null;
				try {
					propEvalSuccess = TryEvaluatePropertyFunction (context, inv, classRef, out propEvalResult);
				} catch (Exception ex) {
					LoggingService.LogError ("Error in property function evaluation", ex);
				}
				if (propEvalSuccess) {
					return propEvalResult;
				}
				return null;
			}
			if (!prop.IsSimpleProperty) {
				LoggingService.LogWarning ("Only simple properties are supported in imports");
				return null;
			}
			if (context.TryGetProperty (prop.Name, out var v) && v is EvaluatedValue value) {
				return value.EscapedValue;
			}
			return null;
		}

		public static string EvaluatePath (
			this IMSBuildEvaluationContext context,
			ExpressionNode expression,
			string baseDirectory)
			=> MSBuildEscaping.FromMSBuildPath (context.Evaluate(expression).EscapedValue, baseDirectory);

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

		// make sure stringification is kept consistent across all evaluation methods
		static string Stringify (object evaluationResult) => evaluationResult?.ToString ();

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
				if (prop.Expression is ExpressionPropertyFunctionInvocation inv && inv.Target is ExpressionClassReference classRef) {
					bool propEvalSuccess = false;
					object propEvalResult = null;
					try {
						propEvalSuccess = TryEvaluatePropertyFunction (context, inv, classRef, out propEvalResult);
					} catch (Exception ex) {
						LoggingService.LogError ("Error in property function evaluation", ex);
					}
					if (propEvalSuccess) {
						yield return Stringify (propEvalResult);
					}
					yield break;
				}
				if (!prop.IsSimpleProperty) {
					LoggingService.LogWarning ("Only simple properties are supported in imports");
					yield break;
				}
				if (context.TryGetMultivaluedProperty (prop.Name, out var p) && p is OneOrMany<EvaluatedValue> values) {
					foreach (var v in values) {
						yield return prefix + v.EscapedValue;
					}
					break;
				} else if (prefix != null) {
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

		static bool TryEvaluatePropertyFunction(this IMSBuildEvaluationContext context, ExpressionPropertyFunctionInvocation inv, ExpressionClassReference classRef, out object result)
		{
			if (string.Equals (classRef.Name, "MSBuild", StringComparison.OrdinalIgnoreCase)) {
				//var cls = typeof(Microsoft.Build.Evaluation.IntrinsicFunctions);
				//var methods = cls.GetMethods();

				// FIXME populate this
				Microsoft.Build.Shared.BuildEnvironmentHelper.Instance = new Microsoft.Build.Shared.BuildEnvironmentHelper {
					CurrentMSBuildToolsDirectory = "",
					MSBuildExtensionsPath = "",
					MSBuildSDKsPath = "",
					MSBuildToolsDirectory32 = "",
					MSBuildToolsDirectory64 = "",
					VisualStudioInstallRootDirectory = ""
				};

				if (string.Equals (inv.Function.Name, "GetToolsDirectory32")) {
					if (context.TryGetProperty (ReservedProperties.ToolsPath32, out var td) && td is EvaluatedValue toolsDir) {
						result = toolsDir.EscapedValue;
						return true;
					}
				} else if (string.Equals (inv.Function.Name, "GetDirectoryNameOfFileAbove", StringComparison.OrdinalIgnoreCase)) {
					var args = AssertArgsList (inv);
					result = Microsoft.Build.Evaluation.IntrinsicFunctions.GetDirectoryNameOfFileAbove (
						EvaluateNodeAsString (context, args[0]),
						EvaluateNodeAsString (context, args[1]),
						Microsoft.Build.Shared.FileSystem.FileSystems.Default);
					return true;
				} else if (string.Equals (inv.Function.Name, "NormalizePath", StringComparison.OrdinalIgnoreCase)) {
					var args = EvaluateNodesAsStringArray (context, AssertArgsList (inv), 0);
					result = Microsoft.Build.Evaluation.IntrinsicFunctions.NormalizePath (args);
					return true;
				}
				LoggingService.LogWarning ($"Unsupported property function [{classRef.Name}]::{inv.Function.Name}");
			} else if (string.Equals (classRef.Name, "System.IO.Path", StringComparison.OrdinalIgnoreCase)) {
				if (string.Equals (inv.Function.Name, "Combine")) {
					var args = EvaluateNodesAsStringArray (context, AssertArgsList (inv), 0);
					result = System.IO.Path.Combine (args);
					return true;
				} else if (string.Equals (inv.Function.Name, "GetFileNameWithoutExtension", StringComparison.OrdinalIgnoreCase)) {
					var args = AssertArgsList (inv);
					result = System.IO.Path.GetFileNameWithoutExtension (EvaluateNodeAsString (context, args[0]));
					return true;
				}
			}

			LoggingService.LogWarning ($"Unsupported property function [{classRef.Name}]::{inv.Function.Name}");
			result = null;
			return false;
		}

		static List<ExpressionNode> AssertArgsList (ExpressionPropertyFunctionInvocation inv)
		{
			if (inv.Arguments is ExpressionArgumentList list) {
				return list.Arguments;
			}
			throw new NotImplementedException ("Error handling for property function arguments");
		}

		static string[] EvaluateNodesAsStringArray (IMSBuildEvaluationContext context, List<ExpressionNode> arguments, int startIndex)
		{
			var results = new string[arguments.Count - startIndex];
			for (int i = 0; i < results.Length; i++) {
				results[i] = EvaluateNodeAsString (context, arguments[i + startIndex]);
			}
			return results;
		}
	}
}