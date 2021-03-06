// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace MonoDevelop.MSBuild.Language.Expressions
{
	[DebuggerDisplay ("Item: {Name} (IsSimple: {IsSimpleItem})")]
	public class ExpressionItem : ExpressionNode
	{
		public ExpressionNode Expression { get; }

		public bool IsSimpleItem => Expression is ExpressionItemName;
		public string Name => (Expression as ExpressionItemNode)?.ItemName;
		public int? NameOffset => (Expression as ExpressionItemNode)?.ItemNameOffset;

		public ExpressionItem (int offset, int length, ExpressionNode expression) : base (offset, length)
		{
			Expression = expression;
			expression.SetParent (this);
		}

		public ExpressionItem (int offset, string name)
			: this (offset, name.Length + 3, new ExpressionItemName (offset + 2, name))
		{
		}

		public override ExpressionNodeKind NodeKind => ExpressionNodeKind.Item;
	}
}
