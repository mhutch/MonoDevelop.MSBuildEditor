// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP
#nullable enable
#endif

using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Language.Syntax;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Dom
{
	public abstract class MSBuildObject
	{
		protected MSBuildObject (MSBuildElement? parent, ExpressionNode? value)
		{
			Parent = parent;
			Value = value;
		}

		public abstract MSBuildSyntaxKind SyntaxKind { get; }
		public MSBuildElement? Parent { get; private set; }
		public ExpressionNode? Value { get; }

		public abstract TextSpan NameSpan { get; }
	}
}
