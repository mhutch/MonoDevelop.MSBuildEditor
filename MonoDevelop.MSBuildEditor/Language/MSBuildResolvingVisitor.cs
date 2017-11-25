﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.Ide.Editor;
using MonoDevelop.MSBuildEditor.Schema;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuildEditor.Language
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
			if (element.FirstChild != null || resolved.ValueKind == MSBuildValueKind.Data || resolved.ValueKind == MSBuildValueKind.Nothing) {
				return;
			}

			if (element.IsSelfClosing || !(element.ClosingTag is XClosingTag closing)) {
				return;
			}

			var begin = Document.LocationToOffset (element.Region.End);
			int end = Document.LocationToOffset (element.ClosingTag.Region.Begin);
			var value = Document.GetTextBetween (begin, end);

			var info = Context.GetSchemas ().GetElementInfo (resolved, (element.Parent as XElement)?.Name.Name, element.Name.Name, true);
			if (info == null) {
				return;
			}

			VisitValue (info, value, begin);
		}

		protected override void VisitResolvedAttribute (XElement element, XAttribute attribute, MSBuildLanguageElement resolvedElement, MSBuildLanguageAttribute resolvedAttribute)
		{
			VisitAttributeValue (element, attribute, resolvedAttribute);
			base.VisitResolvedAttribute (element, attribute, resolvedElement, resolvedAttribute);
		}

		void VisitAttributeValue (XElement element, XAttribute attribute, MSBuildLanguageAttribute resolvedAttribute)
		{
			if (string.IsNullOrWhiteSpace (attribute.Value)) {
				return;
			}

			var info = Context.GetSchemas ().GetAttributeInfo (resolvedAttribute, element.Name.Name, attribute.Name.Name);

			VisitValue (info, attribute.Value, attribute.GetValueStartOffset (Document));
		}

		protected virtual void VisitValue (ValueInfo info, string value, int offset)
		{
			var kind = MSBuildCompletionExtensions.InferValueKindIfUnknown (info);

			if (!kind.AllowExpressions ()) {
				return;
			}

			var expression = ExpressionParser.Parse (value, kind.GetExpressionOptions (), offset);

			VisitValueExpression (info, kind, expression);
		}

		protected virtual void VisitValueExpression (ValueInfo info, MSBuildValueKind kind, ExpressionNode node)
		{
		}
	}
}