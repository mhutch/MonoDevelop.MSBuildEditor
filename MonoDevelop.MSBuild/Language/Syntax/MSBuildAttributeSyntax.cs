// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using MonoDevelop.MSBuild.Language.Typesystem;

namespace MonoDevelop.MSBuild.Language.Syntax
{
	[DebuggerDisplay("MSBuildAttributeSyntax ({SyntaxKind,nq})")]
	public class MSBuildAttributeSyntax : MSBuildSyntax
	{
		public MSBuildAttributeSyntax (
			MSBuildElementSyntax element,
			string name, DisplayText description, MSBuildSyntaxKind syntaxKind, MSBuildValueKind valueKind,
			CustomTypeInfo? customType = null,
			bool required = false, MSBuildSyntaxKind? abstractKind = null,
			string? deprecationMessage = null,
			string? helpUrl = null)
			: base (name, description, valueKind, customType, deprecationMessage, helpUrl)
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

		[MemberNotNullWhen(true, nameof (AbstractKind))]
		public bool IsAbstract => AbstractKind.HasValue;
    }
}
