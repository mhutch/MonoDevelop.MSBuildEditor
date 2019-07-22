// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace MonoDevelop.MSBuild.Schema
{
	class MSBuildLanguageAttribute : ValueInfo
	{
		public MSBuildLanguageAttribute (
			MSBuildLanguageElement element,
			string name, DisplayText description, MSBuildSyntaxKind syntaxKind, MSBuildValueKind valueKind,
			bool required = false, MSBuildSyntaxKind? abstractKind = null,
			bool isDeprecated = false, string deprecationMessage = null)
			: base (name, description, valueKind, null, null, isDeprecated, deprecationMessage)
		{
			SyntaxKind = syntaxKind;
			Element = element;
			Required = required;
			AbstractKind = abstractKind;
		}

		public MSBuildSyntaxKind SyntaxKind { get; }

		public MSBuildLanguageElement Element { get; }

		public MSBuildSyntaxKind? AbstractKind { get; }
		public bool Required { get; }
		public bool IsAbstract => AbstractKind.HasValue;
    }
}
