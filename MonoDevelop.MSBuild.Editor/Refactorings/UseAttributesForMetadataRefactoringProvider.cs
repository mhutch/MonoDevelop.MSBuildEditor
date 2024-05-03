// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MonoDevelop.MSBuild.Editor.Analysis;
using MonoDevelop.MSBuild.Language.Syntax;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Editor.Refactorings
{
	[Export (typeof (MSBuildRefactoringProvider))]
	class UseAttributesForMetadataRefactoringProvider : MSBuildRefactoringProvider
	{
		public override Task RegisterRefactoringsAsync (MSBuildRefactoringContext context)
		{
			if (context.SelectedSpan.Length > 0) {
				return Task.CompletedTask;
			}

			XElement itemElement;
			switch (context.ElementSyntax?.SyntaxKind) {
			case MSBuildSyntaxKind.Item:
				itemElement = context.XObject.SelfAndParentsOfType<XElement> ().First ();
				break;
			case MSBuildSyntaxKind.Metadata:
				itemElement = context.XObject.SelfAndParentsOfType<XElement> ().Skip (1).First ();
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

			context.RegisterRefactoring (new UseAttributeForMetadataAction (itemElement));

			return Task.CompletedTask;
		}

		static bool IsTransformable (XElement item)
		{
			if (!item.IsClosed || item.IsSelfClosing) {
				return false;
			}

			bool foundAny = false;

			// we can only tranform the item if its only children are metadata elements without attributes
			foreach (var node in item.Nodes) {
				if (!(node is XElement meta) || meta.Attributes.First != null) {
					return false;
				}

				// if the metadata element has a child, it must be a single text node
				if (meta.FirstChild != null && (!(meta.FirstChild is XText t) || t.NextSibling != null)) {
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

		class UseAttributeForMetadataAction : SimpleMSBuildCodeAction
		{
			readonly XElement itemElement;

			public UseAttributeForMetadataAction (XElement itemElement)
			{
				this.itemElement = itemElement;
			}

			public override string Title => $"Use attributes for metadata";
			protected override MSBuildCodeActionOperation CreateOperation ()
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

				return new EditTextActionOperation ()
					.Replace (
						TextSpan.FromBounds (insertionPoint, itemElement.ClosingTag.Span.End),
						sb.ToString ()
					);
			}
		}
	}
}
