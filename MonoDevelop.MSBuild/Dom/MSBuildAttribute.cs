// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Language.Syntax;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Dom
{
	public class MSBuildAttribute : MSBuildObject
	{
		internal MSBuildAttribute nextSibling;

		internal MSBuildAttribute (MSBuildElement parent, XAttribute xattribute, MSBuildAttributeSyntax syntax, ExpressionNode value)
			: base (parent, value)
		{
			XAttribute = xattribute;
			Syntax = syntax;
			Debug.Assert (xattribute.IsNamed && !xattribute.Name.HasPrefix);
		}

		public MSBuildAttributeSyntax Syntax { get; }
		public XAttribute XAttribute { get; }

		public override MSBuildSyntaxKind SyntaxKind => Syntax.SyntaxKind;

		public string Name => XAttribute.Name.Name;

		public bool IsNamed (string name) => string.Equals (Name, name, System.StringComparison.OrdinalIgnoreCase);
	}

	public static class MSBuildDomExtensions
	{
		public static bool? AsConstBool (this MSBuildObject o) =>
			o.Value switch {
				ExpressionArgumentBool b => b.Value,
				ExpressionText t when string.Equals (t.Value, "True", System.StringComparison.OrdinalIgnoreCase) => true,
				ExpressionText t when string.Equals (t.Value, "False", System.StringComparison.OrdinalIgnoreCase) => false,
				//FIXME: the cast is necessary in 16.3 csc, which we currently use on CI
				_ => (bool?)null
			};

		public static string AsConstString (this MSBuildObject o) =>
			o.Value switch {
				ExpressionText t => t.Value,
				_ => null
			};
	}
}
