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

		public ExpressionError (int offset, bool wasEOF, ExpressionErrorKind kind, int length, out bool hasError) : base (offset, length)
		{
			// this exists so callers don't forget to set it
			// having this argument has caught a bunch of issues
			hasError = true;

			Kind = kind;
			WasEOF = wasEOF;
		}

		public ExpressionError (int offset, bool wasEOF, ExpressionErrorKind kind, out bool hasError) : this (offset, wasEOF, kind, wasEOF ? 0 : 1, out hasError)
		{
		}

		public ExpressionError (int offset, ExpressionErrorKind kind, out bool hasError) : this (offset, false, kind, out hasError)
		{
		}

		public override ExpressionNodeKind NodeKind => ExpressionNodeKind.Error;
	}

	[DebuggerDisplay ("Error ({Kind}): {IncompleteNode}")]
	class IncompleteExpressionError : ExpressionError
	{
		public ExpressionNode IncompleteNode { get; }

		public IncompleteExpressionError (int offset, bool wasEOF, ExpressionErrorKind kind, ExpressionNode incompleteNode, out bool hasError)
			: base (incompleteNode.Offset, wasEOF, kind, Math.Max (incompleteNode.Length, offset - incompleteNode.Offset), out hasError)
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
		IncompleteOperator,
		ExpectingEquals,
		IncompleteOrUnsupportedEntity
	}
}
