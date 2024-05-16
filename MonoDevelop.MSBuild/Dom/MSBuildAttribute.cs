// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP
#nullable enable
#endif

using System.Diagnostics;

using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Language.Syntax;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Dom
{
	public class MSBuildAttribute : MSBuildObject
	{
		internal MSBuildAttribute? nextSibling;

		internal MSBuildAttribute (MSBuildElement parent, XAttribute xattribute, MSBuildAttributeSyntax syntax, ExpressionNode? value)
			: base (parent, value)
		{
			Debug.Assert (xattribute.IsNamed && !xattribute.Name.HasPrefix);

			XAttribute = xattribute;
			Syntax = syntax;
			Debug.Assert (xattribute.IsNamed && !xattribute.Name.HasPrefix);
		}

		public MSBuildAttributeSyntax Syntax { get; }

		public XAttribute XAttribute { get; }

		public new MSBuildElement Parent => base.Parent!;

		public override MSBuildSyntaxKind SyntaxKind => Syntax.SyntaxKind;

		public string Name => XAttribute.Name.Name!;
		public override TextSpan NameSpan => XAttribute.NameSpan;

		public bool IsNamed (string name) => string.Equals (Name, name, System.StringComparison.OrdinalIgnoreCase);
	}
}
