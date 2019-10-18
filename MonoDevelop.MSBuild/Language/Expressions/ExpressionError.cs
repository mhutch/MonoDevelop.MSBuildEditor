// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;

namespace MonoDevelop.MSBuild.Language.Expressions
{
	[DebuggerDisplay ("Error ({Kind})")]
	class ExpressionError : ExpressionNode
	{
		public ExpressionErrorKind Kind { get; }
		public bool WasEOF { get; }

		public ExpressionError (int offset, bool wasEOF, ExpressionErrorKind kind, int length) : base (offset, length)
		{
			Kind = kind;
			WasEOF = wasEOF;
		}

		public ExpressionError (int offset, bool wasEOF, ExpressionErrorKind kind) : this (offset, wasEOF, kind, wasEOF ? 0 : 1)
		{
		}

		public ExpressionError (int offset, ExpressionErrorKind kind) : this (offset, false, kind)
		{
		}

		public override ExpressionNodeKind NodeKind => ExpressionNodeKind.Error;
	}

	[DebuggerDisplay ("Error ({Kind}): {IncompleteNode}")]
	class IncompleteExpressionError : ExpressionError
	{
		public ExpressionNode IncompleteNode { get; }

		public IncompleteExpressionError (int offset, bool wasEOF, ExpressionErrorKind kind, ExpressionNode incompleteNode)
			: base (incompleteNode.Offset, wasEOF, kind, Math.Max (incompleteNode.Length, offset - incompleteNode.Offset))
		{
			IncompleteNode = incompleteNode;
			incompleteNode.SetParent (this);
		}

		public override ExpressionNodeKind NodeKind => ExpressionNodeKind.IncompleteExpressionError;
	}

	enum ExpressionErrorKind
	{
		MetadataDisallowed,
		EmptyListEntry,
		ExpectingItemName,
		ExpectingRightParen,
		ExpectingRightParenOrPeriod,
		ExpectingPropertyName,
		ExpectingMetadataName,
		ExpectingMetadataOrItemName,
		ExpectingRightAngleBracket,
		ExpectingRightParenOrDash,
		ItemsDisallowed,
		ExpectingMethodName,
		ExpectingLeftParen,
		ExpectingRightParenOrComma,
		ExpectingRightParenOrValue,
		ExpectingValue,
		CouldNotParseNumber,
		IncompleteValue,
		ExpectingMethodOrTransform,
		ExpectingBracketColonColon,
		ExpectingClassName,
		ExpectingClassNameComponent,
		IncompleteString,
		IncompleteProperty,
		UnexpectedCharacter,
		IncompleteOperator
	}
}
