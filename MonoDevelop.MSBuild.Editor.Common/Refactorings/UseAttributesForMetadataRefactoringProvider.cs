// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using MonoDevelop.MSBuild.Editor.CodeActions;
using MonoDevelop.MSBuild.Language.Syntax;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Editor.Refactorings
{
	[Export (typeof (MSBuildCodeActionProvider))]
	class UseAttributesForMetadataRefactoringProvider : MSBuildCodeActionProvider
	{
		public override Task RegisterCodeActionsAsync (MSBuildCodeActionContext context)
		{
			if (context.Span.Length > 0 || context.SpanStartXObject is null) {
				return Task.CompletedTask;
			}

			XElement itemElement;
			switch (context.SpanStartElementSyntax?.SyntaxKind) {
			case MSBuildSyntaxKind.Item:
				itemElement = context.SpanStartXObject.SelfAndParentsOfType<XElement> ().First ();
				break;
			case MSBuildSyntaxKind.Metadata:
				itemElement = context.SpanStartXObject.SelfAndParentsOfType<XElement> ().Skip (1).First ();
				break;
			default:
				return Task.CompletedTask;
			}

			// check it isn't in an ItemDefinitionGroup
			if (!(itemElement?.Parent is XElement pe && pe.Name.Equals (MSBuildElementName.ItemGroup, true))) {
				return Task.CompletedTask;
			}

			if (!IsTransformable (itemElement)) {
				return Task.CompletedTask;
			}

			context.RegisterCodeAction (new UseAttributeForMetadataAction (itemElement, context));

			return Task.CompletedTask;
		}

		static bool IsTransformable (XElement item)
		{
			if (!item.IsClosed || item.IsSelfClosing) {
				return false;
			}

			bool foundAny = false;

			// we can only transform the item if its only children are metadata elements without attributes
			foreach (var node in item.Nodes) {
				if (node is not XElement meta || meta.Attributes.First is not null || !meta.IsNamed || meta.Name.HasPrefix) {
					return false;
				}

				// if the metadata element has a child, it must be a single text node
				if (meta.FirstChild != null && (meta.FirstChild is not XText t || t.NextSibling != null)) {
					return false;
				}

				//we also cannot transform if it would conflict with reserved attributes
				if (MSBuildElementSyntax.Item.GetAttribute (meta.Name.Name) is MSBuildAttributeSyntax att && !att.IsAbstract) {
					return false;
				}
				foundAny = true;
			}

			return foundAny;
		}

		class UseAttributeForMetadataAction(XElement itemElement, MSBuildCodeActionContext context) : MSBuildDocumentEditBuilderCodeAction(context)
		{
			public override string Title => $"Use attributes for metadata";

			protected override void BuildEdit (MSBuildDocumentEditBuilder builder, CancellationToken cancellationToken)
			{
				var insertionPoint = (itemElement.Attributes.Last?.Span ?? itemElement.NameSpan).End;

				var sb = new StringBuilder ();
				foreach (var node in itemElement.Nodes) {
					var meta = (XElement)node;
					sb.Append (" ");
					sb.Append (meta.Name);
					sb.Append ("=\"");
					var val = (meta.FirstChild as XText)?.Text ?? "";
					if (val.IndexOf ('\'') < 0) {
						val = val.Replace ("\"", "'");
					} else {
						val = val.Replace ("\"", "&quot;");
					}
					sb.Append (val);
					sb.Append ("\"");
				}

				sb.Append (" />");

				builder.Replace (
					TextSpan.FromBounds (insertionPoint, itemElement.ClosingTag.Span.End),
					sb.ToString ()
				);
			}
		}
	}
}
