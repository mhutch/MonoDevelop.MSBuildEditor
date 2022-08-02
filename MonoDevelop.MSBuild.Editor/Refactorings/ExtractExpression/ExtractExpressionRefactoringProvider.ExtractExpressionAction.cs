// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using MonoDevelop.MSBuild.Editor.Analysis;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Editor.Refactorings.ExtractExpression;

partial class ExtractExpressionRefactoringProvider
{
	class ExtractExpressionAction : SimpleMSBuildCodeAction
	{
		readonly string expr;
		readonly TextSpan sourceSpan;
		readonly string propertyName;
		private readonly string? scopeName;
		readonly TextSpan insertionSpan;
		readonly int indentDepth;
		readonly bool createPropertyGroup;

		public ExtractExpressionAction (string expr, TextSpan sourceSpan, string propertyName, string? scopeName, TextSpan insertionSpan, int indentDepth, bool createPropertyGroup)
		{
			this.expr = expr;
			this.sourceSpan = sourceSpan;
			this.propertyName = propertyName;
			this.scopeName = scopeName;
			this.insertionSpan = insertionSpan;
			this.indentDepth = indentDepth;
			this.createPropertyGroup = createPropertyGroup;
		}

		public override string Title =>
			scopeName == null
				? $"Extract expression"
				: $"Extract expression to {scopeName} scope";

		protected override MSBuildCodeActionOperation CreateOperation ()
			=> new EditTextActionOperation ()
			.ReplaceAndSelect (
				insertionSpan,
				createPropertyGroup
					? $"\n<PropertyGroup>\n\t<|{propertyName}|>{expr}</|{propertyName}|>\n</PropertyGroup>\n\n"
					: $"\n<|{propertyName}|>{expr}</|{propertyName}|>\n",
				selectionMarker: '|', baseIndentDepth: indentDepth)
			.ReplaceAndSelect (
				sourceSpan,
				$"$(|{propertyName}|)",
				selectionMarker: '|', baseIndentDepth: indentDepth);
	}
}