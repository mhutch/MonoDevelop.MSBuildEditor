// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using MonoDevelop.MSBuild.Schema;

namespace MonoDevelop.MSBuild.Language.Expressions
{
	static class ExpressionExtensions
	{
		public static IEnumerable<ExpressionNode> WithAllDescendants (this ExpressionNode node)
		{
			yield return node;

			switch (node) {
			case ListExpression list:
				foreach (var c in list.Nodes) {
					foreach (var n in c.WithAllDescendants ()) {
						yield return n;
					}
				}
				break;
			case ConcatExpression expr:
				foreach (var c in expr.Nodes) {
					foreach (var n in c.WithAllDescendants ()) {
						yield return n;
					}
				}
				break;
			case ExpressionItem item:
				if (item.Expression != null) {
					foreach (var n in item.Expression.WithAllDescendants ()) {
						yield return n;
					}
				}
				break;
			case ExpressionProperty property:
				if (property.Expression != null) {
					foreach (var n in property.Expression.WithAllDescendants ()) {
						yield return n;
					}
				}
				break;
			case ExpressionPropertyFunctionInvocation invocation:
				if (invocation.Function != null) {
					yield return invocation.Function;
				}
				if (invocation.Target != null) {
					foreach (var n in invocation.Target.WithAllDescendants ()) {
						yield return n;
					}
				}
				if (invocation.Arguments != null) {
					foreach (var n in invocation.Arguments.WithAllDescendants ()) {
						yield return n;
					}
				}
				break;
			case ExpressionArgumentList argumentList:
				if (argumentList.Arguments != null) {
					foreach (var a in argumentList.Arguments) {
						foreach (var n in a.WithAllDescendants ()) {
							yield return n;
						}
					}
				}
				break;
			case ExpressionItemFunctionInvocation invocation:
				if (invocation.Function != null) {
					yield return invocation.Function;
				}
				if (invocation.Target != null) {
					foreach (var n in invocation.Target.WithAllDescendants ()) {
						yield return n;
					}
				}
				if (invocation.Arguments != null) {
					foreach (var n in invocation.Arguments.WithAllDescendants ()) {
						yield return n;
					}
				}
				break;
			case ExpressionItemTransform transform:
				if (transform.Target != null) {
					foreach (var n in transform.Target.WithAllDescendants ()) {
						yield return n;
					}
				}
				if (transform.Transform != null) {
					foreach (var n in transform.Transform.WithAllDescendants ()) {
						yield return n;
					}
				}
				if (transform.Separator != null) {
					foreach (var n in transform.Separator.WithAllDescendants ()) {
						yield return n;
					}
				}
				break;
			case IncompleteExpressionError err:
				if (err.IncompleteNode != null) {
					foreach (var n in err.IncompleteNode.WithAllDescendants ()) {
						yield return n;
					}
				}
				break;
			case QuotedExpression quotedExpr:
				if (quotedExpr.Expression != null) {
					foreach (var n in quotedExpr.Expression.WithAllDescendants ()) {
						yield return n;
					}
				}
				break;
			}
		}

		public static bool ContainsOffset (this ExpressionNode node, int offset)
		{
			return node.Offset <= offset && offset <= node.End;
		}

		public static ExpressionNode Find (this ExpressionNode node, int offset)
		{
			return node.ContainsOffset (offset) ? FindInternal (node, offset) : null;
		}

		static ExpressionNode FindInternal (this ExpressionNode node, int offset)
		{
			switch (node) {
			case IContainerExpression expr:
				//TODO: binary search?
				foreach (var c in expr.Nodes) {
					var n = (c as IncompleteExpressionError)?.IncompleteNode ?? c;
					if (n.ContainsOffset (offset)) {
						return n.FindInternal (offset);
					}
				}
				break;
			case ExpressionItem item:
				if (item.Expression != null && item.Expression.ContainsOffset (offset)) {
					return item.Expression.FindInternal (offset);
				}
				break;
			case ExpressionProperty prop:
				if (prop.Expression != null && prop.Expression.ContainsOffset (offset)) {
					return prop.Expression.FindInternal (offset);
				}
				break;
			case IncompleteExpressionError err:
				if (err.IncompleteNode != null && err.IncompleteNode.ContainsOffset (offset)) {
					return err.IncompleteNode.FindInternal (offset);
				}
				break;
			case ExpressionPropertyFunctionInvocation prop:
				if (prop.Function != null && prop.Function.ContainsOffset (offset)) {
					return prop.Function;
				}
				if (prop.Target != null && prop.Target.ContainsOffset (offset)) {
					return prop.Target.FindInternal (offset);
				}
				if (prop.Arguments != null && prop.Arguments.ContainsOffset (offset)) {
					return prop.Arguments.FindInternal (offset);
				}
				break;
			case ExpressionArgumentList argumentList:
				if (argumentList.Arguments != null) {
					//TODO: binary search?
					for (int i = argumentList.Arguments.Count - 1; i >= 0; i--){
						var c = argumentList.Arguments[i];
						if (c.ContainsOffset (offset)) {
							return c.FindInternal (offset);
						}
					}
				}
				break;
			case ExpressionItemFunctionInvocation invocation:
				if (invocation.Function != null && invocation.Function.ContainsOffset (offset)) {
					return invocation.Function;
				}
				if (invocation.Target != null && invocation.Target.ContainsOffset (offset)) {
					return invocation.Target.FindInternal (offset);
				}
				if (invocation.Arguments != null && invocation.Arguments.ContainsOffset (offset)) {
					return invocation.Arguments.FindInternal (offset);
				}
				break;
			case ExpressionItemTransform transform:
				if (transform.Target != null && transform.Target.ContainsOffset (offset)) {
					return transform.Target.FindInternal (offset);
				}
				if (transform.Transform != null && transform.Transform.ContainsOffset (offset)) {
					return transform.Transform.FindInternal (offset);
				}
				if (transform.Separator != null && transform.Separator.ContainsOffset (offset)) {
					return transform.Separator.FindInternal (offset);
				}
				break;
			case ExpressionError error:
				if (error.ContainsOffset (offset)) {
					return error;
				}
				break;
			case ExpressionConditionOperator cond:
				if (cond.Left != null && cond.Left.ContainsOffset (offset)) {
					return cond.Left.FindInternal (offset);
				}
				if (cond.Right != null && cond.Right.ContainsOffset (offset)) {
					return cond.Right.FindInternal (offset);
				}
				break;
			case ExpressionConditionFunction condFunc:
				if (condFunc.Name != null && condFunc.Name.ContainsOffset (offset)) {
					return condFunc.Name;
				}
				if (condFunc.Arguments != null && condFunc.Arguments.ContainsOffset (offset)) {
					return condFunc.Arguments.FindInternal (offset);
				}
				break;
			case QuotedExpression quotedExpr:
				if (quotedExpr.Expression != null && quotedExpr.Expression.ContainsOffset (offset)) {
					return quotedExpr.Expression.FindInternal (offset);
				}
				break;
			}
			return node;
		}

		public static string GetMessage (this ExpressionErrorKind errorKind, ValueInfo info, out bool isWarning)
		{
			isWarning = false;
			switch (errorKind) {
			case ExpressionErrorKind.MetadataDisallowed:
				return $"{Name ()} does not allow metadata";
			case ExpressionErrorKind.EmptyListEntry:
				isWarning = true;
				return $"Empty list value";
			case ExpressionErrorKind.ExpectingItemName:
				return $"Expecting item name";
			case ExpressionErrorKind.ExpectingRightParen:
				return $"Expecting ')'";
			case ExpressionErrorKind.ExpectingRightParenOrPeriod:
				return $"Expecting ')' or '.'";
			case ExpressionErrorKind.ExpectingPropertyName:
				return $"Expecting property name";
			case ExpressionErrorKind.ExpectingMetadataName:
				return $"Expecting metadata name";
			case ExpressionErrorKind.ExpectingMetadataOrItemName:
				return $"Expecting metadata or item name";
			case ExpressionErrorKind.ExpectingRightAngleBracket:
				return $"Expecting '>'";
			case ExpressionErrorKind.ExpectingRightParenOrDash:
				return $"Expecting '-' or ')'";
			case ExpressionErrorKind.ItemsDisallowed:
				return $"{Name ()} does not allow metadata";
			case ExpressionErrorKind.ExpectingMethodOrTransform:
				return $"Expecting item function or transform";
			case ExpressionErrorKind.ExpectingMethodName:
				return "Expecting method name";
			case ExpressionErrorKind.ExpectingLeftParen:
				return "Expecting '('";
			case ExpressionErrorKind.ExpectingRightParenOrComma:
				return "Expecting ')' or ','";
			case ExpressionErrorKind.ExpectingRightParenOrValue:
				return "Expecting ',' or value";
			case ExpressionErrorKind.ExpectingValue:
				return "Expecting value";
			case ExpressionErrorKind.CouldNotParseNumber:
				return "Invalid numeric value";
			case ExpressionErrorKind.IncompleteValue:
				return "Incomplete value";
			case ExpressionErrorKind.ExpectingBracketColonColon:
				return "Expecting ']::'";
			case ExpressionErrorKind.ExpectingClassName:
				return "Expecting class name";
			case ExpressionErrorKind.ExpectingClassNameComponent:
				return "Incomplete class name";
			case ExpressionErrorKind.IncompleteString:
				return "Incomplete string";
			case ExpressionErrorKind.IncompleteProperty:
				return "Incomplete property";
			default:
				return $"Invalid expression: {errorKind}";
			}

			string Name () => DescriptionFormatter.GetTitleCaseKindName (info);
		}
	}
}
