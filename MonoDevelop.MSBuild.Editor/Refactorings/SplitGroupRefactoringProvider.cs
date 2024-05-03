// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;

using MonoDevelop.MSBuild.Editor.Analysis;
using MonoDevelop.MSBuild.Language.Syntax;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Editor.Refactorings
{
	[Export (typeof (MSBuildRefactoringProvider))]
	class SplitGroupRefactoringProvider : MSBuildRefactoringProvider
	{
		public override Task RegisterRefactoringsAsync (MSBuildRefactoringContext context)
		{
			if (context.SelectedSpan.Length > 0) {
				return Task.CompletedTask;
			}

			XElement element;
			switch (context.ElementSyntax?.SyntaxKind) {
			case MSBuildSyntaxKind.Item:
				element = context.XObject.SelfAndParentsOfType<XElement> ().First ();
				break;
			case MSBuildSyntaxKind.Metadata:
				element = context.XObject.SelfAndParentsOfType<XElement> ().Skip (1).First ();
				break;
			case MSBuildSyntaxKind.Property:
				element = context.XObject.SelfAndParentsOfType<XElement> ().First ();
				break;
			default:
				return Task.CompletedTask;
			}

			var group = (XElement)element.Parent;
			XElement previousElement = null;
			foreach (var c in group.Elements) {
				if (c == element) {
					break;
				}
				previousElement = c;
			}

			if (previousElement == null) {
				return Task.CompletedTask;
			}

			//check name is cased correctly
			var groupName = MSBuildElementSyntax.Get (group)?.Name;

			context.RegisterRefactoring (new SplitGroupAction (previousElement, groupName));

			return Task.CompletedTask;
		}

		class SplitGroupAction : SimpleMSBuildCodeAction
		{
			readonly XElement previousElement;
			private readonly string groupName;

			public SplitGroupAction (XElement afterElement, string groupName)
			{
				this.previousElement = afterElement;
				this.groupName = groupName;
			}

			public override string Title => $"Split {groupName}";
			protected override MSBuildCodeActionOperation CreateOperation ()
			{
				//insert after the end of the previous element, so we bring along any comment
				var replaceSpan = TextSpan.FromBounds (
					previousElement.OuterSpan.End,
					previousElement.NextSibling.Span.Start
				);

				//FIXME: better indentation
				return new EditTextActionOperation ()
					.Replace (replaceSpan, $"\n  </{groupName}>\n\n  <{groupName}>\n    ");
			}
		}
	}
}
