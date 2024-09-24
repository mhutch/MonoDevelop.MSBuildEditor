// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using MonoDevelop.MSBuild.Editor.CodeActions;
using MonoDevelop.MSBuild.Language.Syntax;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Editor.Refactorings
{
	[Export (typeof (MSBuildCodeActionProvider))]
	class SplitGroupRefactoringProvider : MSBuildCodeActionProvider
	{
		public override Task RegisterCodeActionsAsync (MSBuildCodeActionContext context)
		{
			if (context.Span.Length > 0 || context.SpanStartXObject is null) {
				return Task.CompletedTask;
			}

			XElement element;
			switch (context.SpanStartElementSyntax?.SyntaxKind) {
			case MSBuildSyntaxKind.Item:
				element = context.SpanStartXObject.SelfAndParentsOfType<XElement> ().First ();
				break;
			case MSBuildSyntaxKind.Metadata:
				element = context.SpanStartXObject.SelfAndParentsOfType<XElement> ().Skip (1).First ();
				break;
			case MSBuildSyntaxKind.Property:
				element = context.SpanStartXObject.SelfAndParentsOfType<XElement> ().First ();
				break;
			default:
				return Task.CompletedTask;
			}

			// any code that results in one of the above SyntaxKinds must have a a parent
			var group = (XElement)element.Parent!;

			XElement? previousElement = null;
			foreach (var c in group.Elements) {
				if (c == element) {
					break;
				}
				previousElement = c;
			}

			if (previousElement == null) {
				return Task.CompletedTask;
			}

			// ensure name is cased correctly by using the name from the syntax definition, not the literal name from the document
			var groupName = MSBuildElementSyntax.Get (group)!.Name;

			context.RegisterCodeAction(new SplitGroupAction (previousElement, groupName, context));

			return Task.CompletedTask;
		}

		class SplitGroupAction(XElement previousElement, string groupName, MSBuildCodeActionContext context) : MSBuildDocumentEditBuilderCodeAction(context)
		{
			public override string Title => $"Split {groupName}";
			protected override void BuildEdit (MSBuildDocumentEditBuilder builder, CancellationToken cancellationToken)
			{
				//insert after the end of the previous element, so we bring along any comment
				var replaceSpan = TextSpan.FromBounds (
					previousElement.OuterSpan.End,
					previousElement.NextSibling.Span.Start
				);

				//FIXME: better indentation
				builder.Replace (replaceSpan, $"\n  </{groupName}>\n\n  <{groupName}>\n    ");
			}
		}
	}
}
