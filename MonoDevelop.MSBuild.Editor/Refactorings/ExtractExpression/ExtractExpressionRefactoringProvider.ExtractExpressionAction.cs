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
		readonly string indent;
		readonly bool createPropertyGroup;

		public ExtractExpressionAction (string expr, TextSpan sourceSpan, string propertyName, string? scopeName, TextSpan insertionSpan, int indentDepth, bool createPropertyGroup)
		{
			this.expr = expr;
			this.sourceSpan = sourceSpan;
			this.propertyName = propertyName;
			this.scopeName = scopeName;
			this.insertionSpan = insertionSpan;
			this.indent = new string(' ', indentDepth * 2);
			this.createPropertyGroup = createPropertyGroup;
		}

		public override string Title =>
			scopeName == null
				? $"Extract expression"
				: $"Extract expression to {scopeName} scope";

		EditTextActionOperation.Edit GetNewPropertyWithGroupEdit () => new (
			EditTextActionOperation.EditKind.Replace,
			insertionSpan,
			$"\n{indent}<PropertyGroup>\n{indent}  <{propertyName}>{expr}</{propertyName}>\n{indent}</PropertyGroup>\n\n{indent}",
			new[] {
				new TextSpan (20 + indent.Length*2, propertyName.Length),
				new TextSpan (20 + indent.Length*2 + propertyName.Length + 1 + expr.Length + 2, propertyName.Length)
			}
		);

		EditTextActionOperation.Edit GetNewPropertyEdit () => new (
			EditTextActionOperation.EditKind.Replace,
			insertionSpan,
			$"\n{indent}<{propertyName}>{expr}</{propertyName}>\n{indent}",
			new[] {
				new TextSpan (2 + indent.Length, propertyName.Length),
				new TextSpan (2 + indent.Length + propertyName.Length + 1 + expr.Length + 2, propertyName.Length)
			}
		);

		protected override MSBuildCodeActionOperation CreateOperation ()
			=> new EditTextActionOperation ()
			.WithEdit (createPropertyGroup
				? GetNewPropertyWithGroupEdit ()
				: GetNewPropertyEdit ())
			.Replace (
				sourceSpan.Start, sourceSpan.Length, $"$({propertyName})",
				relativeSelections: new[] {
					new TextSpan (2, propertyName.Length),
				}
			);
	}
}