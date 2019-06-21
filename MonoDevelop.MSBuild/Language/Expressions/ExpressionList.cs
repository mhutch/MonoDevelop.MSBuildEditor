// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Diagnostics;

namespace MonoDevelop.MSBuild.Language.Expressions
{
	[DebuggerDisplay ("ExpressionList ({Nodes.Count} nodes)")]
	class ComplexExpression : ExpressionNode
	{
		public IReadOnlyList<ExpressionNode> Nodes { get; }

		public ComplexExpression (int offset, int length, params ExpressionNode [] nodes) : base (offset, length)
		{
			Nodes = nodes;

			foreach (var n in nodes) {
				n.SetParent (this);
			}
		}
	}
}
