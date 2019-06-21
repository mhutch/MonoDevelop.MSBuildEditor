// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace MonoDevelop.MSBuild.Language.Expressions
{
	abstract class ExpressionNode
	{
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
    }
}
