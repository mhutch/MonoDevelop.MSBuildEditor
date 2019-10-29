// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


using System.Diagnostics;

namespace MonoDevelop.MSBuild.Language.Expressions
{
	class ExpressionConditionOperator : ExpressionNode
	{
		public ExpressionNode Left { get; }
		public ExpressionNode Right { get; }
		public ExpressionOperatorKind? OperatorKind { get; }

		public override ExpressionNodeKind NodeKind => ExpressionNodeKind.ConditionOperator;

		ExpressionConditionOperator (int offset, ExpressionNode operand)
			: base (offset, operand.Length + (operand.Offset - offset))
		{
			OperatorKind = ExpressionOperatorKind.Not;
			Left = operand;
			operand?.SetParent (this);
		}

		public static ExpressionConditionOperator Not (int offset, ExpressionNode operand)
			=> new ExpressionConditionOperator (offset, operand);

		public ExpressionConditionOperator (ExpressionOperatorKind? comparisonKind, ExpressionNode left, ExpressionNode right)
			: base (left.Offset, right != null? (right.Offset + right.Length - left.Offset) : left.Length)
		{
			Debug.Assert (comparisonKind != ExpressionOperatorKind.Not);
			OperatorKind = comparisonKind;
			Left = left;
			left?.SetParent (this);
			Right = right;
			right?.SetParent (this);
		}
	}

	enum ExpressionOperatorKind
	{
		Equal,
		NotEqual,
		LessThan,
		LessThanOrEqual,
		GreaterThan,
		GreaterThanOrEqual,
		And,
		Or,
		Not
	}

	class ExpressionConditionFunction : ExpressionNode
	{
		public ExpressionFunctionName Name { get; }

		public ExpressionNode Arguments { get; }

		public override ExpressionNodeKind NodeKind => ExpressionNodeKind.ConditionFunction;

		public ExpressionConditionFunction (int offset, int length, string name, ExpressionNode arguments)
			: this (offset, length, new ExpressionFunctionName (offset, name), arguments) { }

		public ExpressionConditionFunction (int offset, int length, ExpressionFunctionName name, ExpressionNode arguments)
			: base (offset, length)
		{
			Name = name;
			name?.SetParent (this);
			Arguments = arguments;
			arguments?.SetParent (this);
		}
	}
}
