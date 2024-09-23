// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System.Collections.Generic;
using System.Linq;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Editor.Refactorings.ExtractExpression;

static class ExpressionNodeExtraction
{
	public static TextSpan? GetValidExtractionSpan (TextSpan selection, ExpressionNode expression)
	{
		if (expression.Find (selection.Start)?.ExpandToExtractableNode () is not ExpressionNode startNode) {
			return null;
		}

		if (expression.Find (selection.End)?.ExpandToExtractableNode () is not ExpressionNode endNode) {
			return null;
		}

		if (startNode == endNode && !startNode.HasForbiddenChild ()) {
			return CreateExtractionSpanFromValidatedNodes (startNode, endNode, selection);
		}

		if (startNode.Parent == endNode.Parent && startNode.Parent is ConcatExpression cat && !cat.HasForbiddenChildBetweenOffsets (startNode.Offset, endNode.End)) {
			return CreateExtractionSpanFromValidatedNodes (startNode, endNode, selection);
		}

		return null;
	}

	static TextSpan CreateExtractionSpanFromValidatedNodes (ExpressionNode startNode, ExpressionNode endNode, TextSpan selection)
		=> TextSpan.FromBounds (
			startNode is ExpressionText startText && startText.Span.Contains (selection.Start) ? selection.Start : startNode.Span.Start,
			endNode is ExpressionText endText && endText.Span.ContainsOuter (selection.End) ? selection.End : endNode.Span.End
		);

	static bool HasForbiddenChild (this ExpressionNode node) => node.WithAllDescendants ().Any (IsForbiddenNode);

	static bool HasForbiddenChildBetweenOffsets (this ExpressionNode node, int startOffset, int endOffset) => node.WithAllDescendants ().BetweenOffsets (startOffset, endOffset).Any (IsForbiddenNode);

	static IEnumerable<ExpressionNode> BetweenOffsets (this IEnumerable<ExpressionNode> nodes, int start, int end) => nodes.Where (n => n.Offset >= start && n.End <= end);

	static bool IsForbiddenNode (this ExpressionNode n) => n.NodeKind switch {
		ExpressionNodeKind.IncompleteExpressionError => true,
		ExpressionNodeKind.Error => true,
		// we need to do more work to figure out when metadata can be extracted
		ExpressionNodeKind.Metadata => true,
		_ => false
	};

	/// <summary>
	/// If the node is not extractable, but a parent is, return that parent
	/// </summary>
	static ExpressionNode? ExpandToExtractableNode (this ExpressionNode? node) => node?.NodeKind switch {
		ExpressionNodeKind.IncompleteExpressionError
			or ExpressionNodeKind.Error
			or ExpressionNodeKind.ConditionOperator
			or ExpressionNodeKind.ConditionFunction
			or ExpressionNodeKind.ParenGroup => null,
		ExpressionNodeKind.ItemFunctionInvocation
			or ExpressionNodeKind.PropertyFunctionInvocation
			or ExpressionNodeKind.PropertyRegistryValue
			or ExpressionNodeKind.ClassReference
			or ExpressionNodeKind.FunctionName
			or ExpressionNodeKind.ItemTransform
			or ExpressionNodeKind.ItemName
			or ExpressionNodeKind.PropertyName
			or ExpressionNodeKind.ArgumentList => node.Parent?.ExpandToExtractableNode (),
		ExpressionNodeKind.ArgumentLiteralBool
			or ExpressionNodeKind.ArgumentLiteralInt
			or ExpressionNodeKind.ArgumentLiteralFloat
			or ExpressionNodeKind.ArgumentLiteralString
			or ExpressionNodeKind.Item
			or ExpressionNodeKind.Concat
			or ExpressionNodeKind.Metadata
			or ExpressionNodeKind.List
			or ExpressionNodeKind.Text
			or ExpressionNodeKind.Property
			or ExpressionNodeKind.QuotedExpression => node,
		_ => null,
	};
}