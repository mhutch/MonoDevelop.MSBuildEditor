// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Language.Syntax;

namespace MonoDevelop.MSBuild.Dom
{
	public abstract class MSBuildObject
	{
		protected MSBuildObject (MSBuildElement parent, ExpressionNode value)
		{
			Parent = parent;
			Value = value;
		}

		public abstract MSBuildSyntaxKind SyntaxKind { get; }
		public MSBuildElement Parent { get; private set; }
		public ExpressionNode Value { get; }
	}
}
