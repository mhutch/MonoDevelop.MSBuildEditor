// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Language
{
	abstract class MSBuildResolvingVisitor : MSBuildVisitor
	{
		protected override void VisitResolvedElement (XElement element, MSBuildLanguageElement resolved)
		{
			base.VisitResolvedElement (element, resolved);

			VisitElementValue (element, resolved);
		}

		void VisitElementValue (XElement element, MSBuildLanguageElement resolved)
		{
			if (resolved.ValueKind == MSBuildValueKind.Data || resolved.ValueKind == MSBuildValueKind.Nothing || element.IsSelfClosing) {
				return;
			}

			//FIXME: handle multiple text nodes with comments between them
			string value = string.Empty;
			var begin = element.Span.End;
			var textNode = element.Nodes.OfType<XText> ().FirstOrDefault ();
			if (textNode != null) {
				begin = textNode.Span.Start;
				value = TextSource.GetTextBetween (begin, textNode.Span.End);
			}

			var info = Document.GetSchemas ().GetElementInfo (resolved, (element.Parent as XElement)?.Name.Name, element.Name.Name, true);
			if (info == null) {
				return;
			}

			VisitValue (element, null, resolved, null, info, value, begin);
		}

		protected override void VisitResolvedAttribute (
			XElement element, XAttribute attribute,
			MSBuildLanguageElement resolvedElement, MSBuildLanguageAttribute resolvedAttribute)
		{
			VisitAttributeValue (element, attribute, resolvedElement, resolvedAttribute);
			base.VisitResolvedAttribute (element, attribute, resolvedElement, resolvedAttribute);
		}

		void VisitAttributeValue (
			XElement element, XAttribute attribute,
			MSBuildLanguageElement resolvedElement, MSBuildLanguageAttribute resolvedAttribute)
		{
			if (string.IsNullOrWhiteSpace (attribute.Value)) {
				return;
			}

			var info = Document.GetSchemas ().GetAttributeInfo (resolvedAttribute, element.Name.Name, attribute.Name.Name);

			if (info == null) {
				return;
			}

			VisitValue (
				element, attribute, resolvedElement, resolvedAttribute,
				info, attribute.Value, attribute.ValueOffset);
		}

		protected virtual void VisitValue (
			XElement element, XAttribute attribute,
			MSBuildLanguageElement resolvedElement, MSBuildLanguageAttribute resolvedAttribute,
			ValueInfo info, string value, int offset)
		{
			var kind = MSBuildCompletionExtensions.InferValueKindIfUnknown (info);

			if (!kind.AllowExpressions ()) {
				VisitValueExpression (
					element, attribute, resolvedElement, resolvedAttribute,
					info, kind, new ExpressionText (offset, value, true));
				return;
			}

			var expression = ExpressionParser.Parse (value, kind.GetExpressionOptions (), offset);

			VisitValueExpression (
				element, attribute, resolvedElement, resolvedAttribute,
				info, kind, expression);
		}

		protected virtual void VisitValueExpression (
			XElement element, XAttribute attribute,
			MSBuildLanguageElement resolvedElement, MSBuildLanguageAttribute resolvedAttribute,
			ValueInfo info, MSBuildValueKind kind, ExpressionNode node)
		{
		}
	}
}
