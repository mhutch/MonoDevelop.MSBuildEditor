// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text;

using Microsoft.Extensions.Logging;

using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Util;

namespace MonoDevelop.MSBuild.Evaluation
{
	static partial class MSBuildEvaluatorExtensions
	{
		public static EvaluatedValue Evaluate (this IMSBuildEvaluationContext context, string expression)
			=> Evaluate (context, ExpressionParser.Parse (expression));

		public static EvaluatedValue Evaluate (this IMSBuildEvaluationContext context, ExpressionNode expression) => new (EvaluateNodeAsString (context, expression));

		static string EvaluateNodeAsString (this IMSBuildEvaluationContext context, ExpressionNode expression) => Stringify (EvaluateNode (context, expression));

		static object EvaluateNode (this IMSBuildEvaluationContext context, ExpressionNode expression)
		{
			switch (expression.NodeKind) {
			case ExpressionNodeKind.Text:
				return ((ExpressionText)expression).Value;

			case ExpressionNodeKind.Property:
				return EvaluateNode (context, ((ExpressionProperty)expression).Expression);

			case ExpressionNodeKind.Concat:
				return EvaluateConcat (context, (ConcatExpression)expression);

			case ExpressionNodeKind.QuotedExpression:
				// TODO: unescaping?
				var quotedExpr = EvaluateNode (context, ((QuotedExpression)expression).Expression);
				return Stringify (quotedExpr);

			case ExpressionNodeKind.PropertyName:
				context.TryGetProperty (((ExpressionPropertyName)expression).Name, out var propertyValue);
				return propertyValue?.EscapedValue;

			case ExpressionNodeKind.PropertyFunctionInvocation:
				TryEvaluatePropertyFunction (context, (ExpressionPropertyFunctionInvocation)expression, out object functionReturn);
				return functionReturn;

			// HACK: TryExecuteWellKnownFunction only supports doubles on Math.Max so coerce preemptively for now
			case ExpressionNodeKind.ArgumentLiteralInt:
				return (double) ((ExpressionArgumentInt)expression).Value;

			case ExpressionNodeKind.ArgumentLiteralFloat:
			case ExpressionNodeKind.ArgumentLiteralBool:
			case ExpressionNodeKind.ArgumentLiteralString:
				return ((ExpressionArgumentLiteral)expression).Value;

			default:
				LogUnsupportedNodeKind (context.Logger, expression.NodeKind);
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
				if (prop.Expression is ExpressionPropertyFunctionInvocation inv) {
					if (TryEvaluatePropertyFunction (context, inv, out object propEvalResult)) {
						yield return Stringify (propEvalResult);
					}
					yield break;
				}
				if (!prop.IsSimpleProperty) {
					LogOnlySupportSimpleProperties (context.Logger);
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
				LogOnlySupportSimplePropertiesAndExpressions (context.Logger);
				yield break;
			}
		}

		static bool TryEvaluatePropertyFunction (this IMSBuildEvaluationContext context, ExpressionPropertyFunctionInvocation inv,  out object result)
		{
			Type receiver = null;
			object instance = null;

			if (inv.Target is ExpressionClassReference classRef) {
				receiver = TryGetStaticFunctionReceiver (classRef);
				if (receiver is null) {
					LogPropertyFunctionsNotSupportedOnType (context.Logger, classRef.Name);
					result = null;
					return false;
				}
			} else {
				instance = EvaluateNode (context, inv.Target);
				if (instance is null) {
					LogCannotInvokeFunctionOnNull (context.Logger);
					result = null;
					return false;
				}
				receiver = instance.GetType ();
			}

			string functionName;
			if (inv.IsIndexer) {
				functionName = IndexerNameForInstance (instance);
			} else {
				functionName = inv.IsProperty ? $"get_{inv.Function.Name}" : inv.Function.Name;
			}

			// FIXME: cache this?
			Microsoft.Build.Shared.BuildEnvironmentHelper.Instance = GetBuildEnvironmentFromContext (context);
			var filesystem = Microsoft.Build.Shared.FileSystem.FileSystems.Default;

			var args = EvaluateArguments (context, inv);

			try {
				// TODO: reflection based dispatch
				if (Microsoft.Build.Evaluation.Expander.Function.TryExecuteWellKnownFunction (receiver, functionName, filesystem, out result, instance, args)) {
					return true;
				}
			} catch (Exception ex) {
				LogErrorInPropertyFunctionEvaluation (context.Logger, ex);
				result = null;
				return false;
			}

			LogUnsupportedPropertyFunction (context.Logger, receiver, inv.Function.Name);
			result = null;
			return false;
		}

		// these are the ones supported by MSBuild's Expander.cs; it doesn't check the indexer attribute
		static string IndexerNameForInstance (object instance) => instance switch {
			Array => "GetValue",
			string => "get_Chars",
			_ => "get_Item"
		};

		static object[] EvaluateArguments (IMSBuildEvaluationContext context, ExpressionPropertyFunctionInvocation inv)
		{
			if (inv.Arguments is ExpressionArgumentList list && list.Arguments is List<ExpressionNode> args) {
				return EvaluateNodesAsArray (context, args);
			}
			return null;
		}

		static object[] EvaluateNodesAsArray (IMSBuildEvaluationContext context, List<ExpressionNode> arguments)
		{
			var results = new object[arguments.Count];
			for (int i = 0; i < results.Length; i++) {
				results[i] = EvaluateNode (context, arguments[i]);
			}
			return results;
		}

		// FIXME: populate this better?
		static Microsoft.Build.Shared.BuildEnvironmentHelper GetBuildEnvironmentFromContext (IMSBuildEvaluationContext context)
		{
			context.TryGetProperty (ReservedProperties.ToolsPath32, out var toolsPath32);
			context.TryGetProperty (ReservedProperties.ToolsPath64, out var toolsPath64);
			context.TryGetProperty (ReservedProperties.ToolsPath, out var toolsPath);
			context.TryGetProperty (ReservedProperties.SDKsPath, out var sdksPath);

			return new Microsoft.Build.Shared.BuildEnvironmentHelper {
				CurrentMSBuildToolsDirectory = toolsPath?.EscapedValue,
				MSBuildExtensionsPath = "",
				MSBuildSDKsPath = sdksPath?.EscapedValue ?? "",
				MSBuildToolsDirectory32 = toolsPath32?.EscapedValue ?? toolsPath?.EscapedValue ?? "",
				MSBuildToolsDirectory64 = toolsPath64?.EscapedValue ?? toolsPath?.EscapedValue ?? "",
				VisualStudioInstallRootDirectory = ""
			};
		}

		static Type TryGetStaticFunctionReceiver (ExpressionClassReference classRef)
		{
			// these are the types supported by TryExecuteWellKnownFunction
			// FIXME: support other types
			if (string.Equals (classRef.Name, "MSBuild", StringComparison.OrdinalIgnoreCase)) {
				return typeof (Microsoft.Build.Evaluation.IntrinsicFunctions);
			} else if (string.Equals (classRef.Name, "System.IO.Path", StringComparison.OrdinalIgnoreCase)) {
				return typeof (System.IO.Path);
			} else if (string.Equals (classRef.Name, "System.Math", StringComparison.OrdinalIgnoreCase)) {
				return typeof (Math);
			} else if (string.Equals (classRef.Name, "System.String", StringComparison.OrdinalIgnoreCase)) {
				return typeof (String);
			} else if (string.Equals (classRef.Name, "System.Version", StringComparison.OrdinalIgnoreCase)) {
				return typeof (Version);
			} else if (string.Equals (classRef.Name, "System.Guid", StringComparison.OrdinalIgnoreCase)) {
				return typeof (Guid);
			}

			return null;
		}

		[LoggerMessage (EventId = 0, Level = LogLevel.Warning, Message = "Evaluator does not currently support expression node type {nodeKind}")]
		static partial void LogUnsupportedNodeKind (ILogger logger, ExpressionNodeKind nodeKind);


		[LoggerMessage (EventId = 1, Level = LogLevel.Warning, Message = "Only simple properties are supported in imports")]
		static partial void LogOnlySupportSimpleProperties (ILogger logger);


		[LoggerMessage (EventId = 2, Level = LogLevel.Warning, Message = "Only simple properties and expressions are supported in imports")]
		static partial void LogOnlySupportSimplePropertiesAndExpressions (ILogger logger);


		[LoggerMessage (EventId = 3, Level = LogLevel.Warning, Message = "Cannot invoke function on null object")]
		static partial void LogCannotInvokeFunctionOnNull (ILogger logger);


		[LoggerMessage (EventId = 4, Level = LogLevel.Warning, Message = "Error in property function evaluation")]
		static partial void LogErrorInPropertyFunctionEvaluation (ILogger logger, Exception ex);


		[LoggerMessage (EventId = 5, Level = LogLevel.Warning, Message = "Unsupported property function '[{receiver}]::{functionName}'")]
		static partial void LogUnsupportedPropertyFunction (ILogger logger, Type receiver, string functionName);


		[LoggerMessage (EventId = 6, Level = LogLevel.Warning, Message = "Property functions not currently supported on type [{className}]")]
		static partial void LogPropertyFunctionsNotSupportedOnType (ILogger logger, string className);
	}
}