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
		readonly bool createPropertyGroup;

		public ExtractExpressionAction (string expr, TextSpan sourceSpan, string propertyName, string? scopeName, TextSpan insertionSpan, bool createPropertyGroup)
		{
			this.expr = expr;
			this.sourceSpan = sourceSpan;
			this.propertyName = propertyName;
			this.scopeName = scopeName;
			this.insertionSpan = insertionSpan;
			this.createPropertyGroup = createPropertyGroup;
		}

		public override string Title =>
			scopeName == null
				? $"Extract expression"
				: $"Extract expression to {scopeName} scope";

		EditTextActionOperation.Edit GetNewPropertyWithGroupEdit () => new (
			EditTextActionOperation.EditKind.Replace,
			insertionSpan,
			$"\n    <PropertyGroup>\n      <{propertyName}>{expr}</{propertyName}>\n    </PropertyGroup>\n    ",
			new[] {
				new TextSpan (28, propertyName.Length),
				new TextSpan (28 + propertyName.Length + 1 + expr.Length + 2, propertyName.Length)
			}
		);

		EditTextActionOperation.Edit GetNewPropertyEdit () => new (
			EditTextActionOperation.EditKind.Replace,
			insertionSpan,
			$"\n    <{propertyName}>{expr}</{propertyName}>\n    ",
			new[] {
				new TextSpan (6, propertyName.Length),
				new TextSpan (6 + propertyName.Length + 1 + expr.Length + 2, propertyName.Length)
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