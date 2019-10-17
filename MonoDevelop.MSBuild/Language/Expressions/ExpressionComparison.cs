// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace MonoDevelop.MSBuild.Language.Expressions
{
	class ExpressionConditionOperator : ExpressionNode
	{
		public ExpressionNode Left { get; }
		public ExpressionNode Right { get; }
		public ExpressionOperatorKind OperatorKind { get; }

		public override ExpressionNodeKind NodeKind => ExpressionNodeKind.ConditionOperator;

		public ExpressionConditionOperator (int offset, int length, ExpressionOperatorKind comparisonKind, ExpressionNode left, ExpressionNode right)
			: base (offset, length)
		{
			OperatorKind = comparisonKind;
			Left = left;
			Right = right;
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

		public ExpressionArgumentList Arguments { get; }

		public override ExpressionNodeKind NodeKind => ExpressionNodeKind.ConditionFunction;

		public ExpressionConditionFunction (int offset, int length, ExpressionFunctionName name, ExpressionArgumentList arguments)
			: base (offset, length)
		{
			Name = name;
			Arguments = arguments;
		}
	}
}
