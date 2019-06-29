// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace MonoDevelop.MSBuild.Language.Expressions
{
	[DebuggerDisplay ("Item: {Name} (IsSimple: {IsSimpleItem})")]
	class ExpressionItem : ExpressionNode
	{
		public ExpressionItemNode Expression { get; }

		public bool IsSimpleItem => Expression is ExpressionItemName;
		public string Name => Expression.ItemName;
		public int? NameOffset => Expression.ItemNameOffset;

		public ExpressionItem (int offset, int length, ExpressionItemNode expression) : base (offset, length)
		{
			Expression = expression;
			expression.SetParent (this);
		}

		public ExpressionItem (int offset, int length, string name)
			: this (offset, length, new ExpressionItemName (offset + 2, name.Length, name))
		{
		}

		public override ExpressionNodeKind NodeKind => ExpressionNodeKind.Item;
	}
}
