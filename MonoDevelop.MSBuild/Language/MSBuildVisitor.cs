// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Language
{
	abstract class MSBuildVisitor
	{
		protected MSBuildDocument Document { get; private set; }
		protected string Filename { get; private set; }
		protected ITextSource TextSource { get; private set; }
		protected string Extension { get; private set; }

		protected bool IsTargetsFile => string.Equals (Extension, ".targets", System.StringComparison.OrdinalIgnoreCase);
		protected bool IsPropsFile => string.Equals (Extension, ".props", System.StringComparison.OrdinalIgnoreCase);

		public void Run (MSBuildRootDocument doc, int offset = 0, int length = 0)
		{
			Run (doc.XDocument, doc.Text, doc, offset, length);
		}

		public void Run (XDocument xDocument, ITextSource textSource, MSBuildDocument doc, int offset = 0, int length = 0)
		{
			Run (xDocument.RootElement, null, textSource, doc, offset, length);
		}

		public void Run (XElement element, MSBuildLanguageElement resolvedElement, ITextSource textSource, MSBuildDocument document, int offset = 0, int length = 0)
		{
			Filename = textSource.FileName;
			Document = document;
			Extension = Filename == null ? ".props" : System.IO.Path.GetExtension (Filename);
			TextSource = textSource;

			range = new TextSpan (offset, length > 0 ? length + offset : int.MaxValue);

			if (resolvedElement != null) {
				VisitResolvedElement (element, resolvedElement);
			} else if (element != null) {
				ResolveAndVisit (element, null);
			}
		}

		TextSpan range;

		void ResolveAndVisit (XElement element, MSBuildLanguageElement parent)
		{
			if (!element.Name.IsValid) {
				return;
			}
			var resolved = MSBuildLanguageElement.Get (element.Name.Name, parent);
			if (resolved != null) {
				VisitResolvedElement (element, resolved);
			} else {
				VisitUnknownElement (element);
			}
		}

		protected virtual void VisitResolvedElement (XElement element, MSBuildLanguageElement resolved)
		{
			ResolveAttributesAndValue (element, resolved);

			if (resolved.ValueKind == MSBuildValueKind.Nothing) {
				foreach (var child in element.Elements) {
					if ((child.ClosingTag ?? child).Span.End < range.Start) {
						continue;
					}
					if (child.Span.Start > range.End) {
						return;
					}
					ResolveAndVisit (child, resolved);
				}
			}
		}

		void ResolveAttributesAndValue (XElement element, MSBuildLanguageElement resolved)
		{
			foreach (var att in element.Attributes) {
				if (att.Span.End < range.Start) {
					continue;
				}
				if (att.Span.Start > range.End) {
					return;
				}
				var resolvedAtt = resolved.GetAttribute (att.Name.FullName);
				if (resolvedAtt != null) {
					VisitResolvedAttribute (element, att, resolved, resolvedAtt);
					continue;
				}
				VisitUnknownAttribute (element, att);
			}

			if (resolved.ValueKind != MSBuildValueKind.Nothing && resolved.ValueKind != MSBuildValueKind.Data) {
				VisitElementValue (element, resolved);
				return;
			}
		}

		protected virtual void VisitResolvedAttribute (
			XElement element, XAttribute attribute,
			MSBuildLanguageElement resolvedElement, MSBuildLanguageAttribute resolvedAttribute)
		{
			if (attribute.Value != null) {
				VisitAttributeValue (element, attribute, resolvedElement, resolvedAttribute, attribute.Value, attribute.GetValueOffset ());
			}
		}

		protected virtual void VisitUnknownElement (XElement element)
		{
		}

		protected virtual void VisitUnknownAttribute (XElement element, XAttribute attribute)
		{
		}

		void VisitElementValue (XElement element, MSBuildLanguageElement resolved)
		{
			if (element.IsSelfClosing || !element.IsEnded) {
				return;
			}

			var begin = element.Span.End;
			int end = begin;

			if (element.IsClosed && element.FirstChild == null) {
				end = element.ClosingTag.Span.Start;
			} else {
				//HACK: in some cases GetCharAt can throw at the end of the document even with TextDocument.Length check
				try {
					for (; end < (TextSource.Length + 1) && TextSource.GetCharAt (end) != '<'; end++) { }
				} catch {
					end--;
				}
			}
			var text = TextSource.GetTextBetween (begin, end);

			VisitElementValue (element, resolved, text, begin);
		}

		protected virtual void VisitElementValue (XElement element, MSBuildLanguageElement resolved, string value, int offset)
		{
		}

		protected virtual void VisitAttributeValue (XElement element, XAttribute attribute, MSBuildLanguageElement resolvedElement, MSBuildLanguageAttribute resolvedAttribute, string value, int offset)
		{
		}
	}
}