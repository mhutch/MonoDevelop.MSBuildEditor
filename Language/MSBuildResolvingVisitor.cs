// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using MonoDevelop.Ide.Editor;
using MonoDevelop.MSBuildEditor.ExpressionParser;
using MonoDevelop.MSBuildEditor.Schema;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuildEditor.Language
{
	abstract class MSBuildResolvingVisitor : MSBuildVisitor
	{
		protected MSBuildResolveContext Context { get; }

		public MSBuildResolvingVisitor (MSBuildResolveContext context)
		{
			Context = context;
		}

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

			//TODO: comma-separated lists
			var expr = new Expression ();
			expr.Parse (value, ParseOptions.AllowItemsMetadataAndSplit);

			VisitValueExpression (info, kind, expr, offset, value.Length);
		}

		protected virtual void VisitValueExpression (ValueInfo info, MSBuildValueKind kind, Expression expression, int offset, int length)
		{
			var scalarKind = kind.GetScalarType ();
			for (int i = 0; i < expression.Collection.Count; i++) {
				var val = expression.Collection [i];
				if (val is string s) {
					//it's a pure value if the items before & ahead of it are list boundaries or ';'
					var isPureLiteralValue = s != ";" &&
						(i == 0 || (expression.Collection [i - 1] is string prev && prev == ";")) &&
						(i + 1 == expression.Collection.Count || (expression.Collection [i + 1] is string next && next == ";"));

					if (isPureLiteralValue) {
						//FIXME: figure out the value offset
						VisitExpressionLiteral (info, scalarKind, s, offset, s.Length);
						continue;
					}
				}
			}
		}

		protected virtual void VisitExpressionLiteral (ValueInfo info, MSBuildValueKind kind, string value, int offset, int length)
		{
		}
	}
}
