// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.MSBuild.Language.Typesystem;

namespace MonoDevelop.MSBuild.Language.Syntax
{
	public class MSBuildAttributeSyntax : MSBuildSyntax
	{
		public MSBuildAttributeSyntax (
			MSBuildElementSyntax element,
			string name, DisplayText description, MSBuildSyntaxKind syntaxKind, MSBuildValueKind valueKind,
			bool required = false, MSBuildSyntaxKind? abstractKind = null,
			string? deprecationMessage = null,
			string? helpUrl = null)
			: base (name, description, valueKind, deprecationMessage, helpUrl)
		{
			SyntaxKind = syntaxKind;
			Element = element;
			Required = required;
			AbstractKind = abstractKind;
		}

		public MSBuildSyntaxKind SyntaxKind { get; }

		public MSBuildElementSyntax Element { get; }

		public MSBuildSyntaxKind? AbstractKind { get; }
		public bool Required { get; }
		public bool IsAbstract => AbstractKind.HasValue;
    }
}
