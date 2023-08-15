// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace MonoDevelop.MSBuild.Language.Expressions
{
	[DebuggerDisplay ("Property: {Name} (IsSimple: {IsSimpleProperty})")]
	public class ExpressionProperty : ExpressionNode
	{
		public ExpressionNode Expression { get; }

		public bool IsSimpleProperty => Expression is ExpressionPropertyName;
		public string? Name => (Expression as ExpressionPropertyName)?.Name;
		public int? NameOffset => (Expression as ExpressionPropertyName)?.Offset;

		public ExpressionProperty (int offset, int length, ExpressionNode expression) : base (offset, length)
		{
			Expression = expression;
			expression.SetParent (this);
		}

		public ExpressionProperty(int offset, string name)
			: this (offset, name.Length + 3, new ExpressionPropertyName (offset + 2, name))
		{
		}

		public override ExpressionNodeKind NodeKind => ExpressionNodeKind.Property;
	}
}
