// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Language.Expressions
{
	abstract class ExpressionNode
	{
		public TextSpan Span => new TextSpan (Offset, Length);

		public int Offset { get; }
		public int Length { get; }
		public int End => Offset + Length;
		public ExpressionNode Parent { get; private set; }

		protected ExpressionNode (int offset, int length)
		{
			Offset = offset;
			Length = length;
		}

		internal void SetParent (ExpressionNode parent) => Parent = parent;

		public abstract ExpressionNodeKind NodeKind { get; }
	}

	enum ExpressionNodeKind
	{
		IncompleteExpressionError,
		Error,
		ArgumentLiteralBool,
		FunctionName,
		ArgumentLiteralInt,
		ArgumentLiteralFloat,
		ArgumentLiteralString,
		Item,
		ItemName,
		ItemFunctionInvocation,
		ItemTransform,
		Concat,
		Metadata,
		PropertyFunctionInvocation,
		PropertyName,
		PropertyRegistryValue,
		ClassReference,
		List,
		Text,
		Property,
		ConditionOperator,
		ConditionFunction,
		QuotedExpression,
		ParenGroup,
		ArgumentList
	}
}
