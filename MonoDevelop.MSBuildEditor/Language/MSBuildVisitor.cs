// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.Core.Text;
using MonoDevelop.Ide.Editor;
using MonoDevelop.MSBuildEditor.Schema;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuildEditor.Language
{
	abstract class MSBuildVisitor
	{
		protected MSBuildDocument Document { get; private set; }
		protected string Filename { get; private set; }
		protected IReadonlyTextDocument TextDocument { get; private set; }

		protected int ConvertLocation (DocumentLocation location) => TextDocument.LocationToOffset (location);

		public void Run (MSBuildRootDocument doc)
		{
			Run (doc.XDocument, doc.Filename, doc.Text, doc);
		}

		public void Run (XDocument xDocument, string filename, ITextSource textDocument, MSBuildDocument doc)
		{
			Run (xDocument.RootElement, null, filename, textDocument, doc);
		}

		public void Run (XElement element, MSBuildLanguageElement resolvedElement, string filename, ITextSource textDocument, MSBuildDocument document)
		{
			Filename = filename;
			Document = document;

			//HACK: we should really use the ITextSource directly, but since the XML parser positions are
			//currently line/col, we need a TextDocument to convert to offsets
			TextDocument = textDocument as IReadonlyTextDocument
				?? TextEditorFactory.CreateNewReadonlyDocument (
					textDocument, filename, MSBuildTextEditorExtension.MSBuildMimeType
				);

			if (resolvedElement != null) {
				VisitResolvedElement (element, resolvedElement);
			} else if (element != null) {
				ResolveAndVisit (element, null);
			}
		}

		void ResolveAndVisit (XElement element, MSBuildLanguageElement parent)
		{
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
					ResolveAndVisit (child, resolved);
				}
			}
		}

		void ResolveAttributesAndValue (XElement element, MSBuildLanguageElement resolved)
		{
			foreach (var att in element.Attributes) {
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
				VisitAttributeValue (element, attribute, resolvedElement, resolvedAttribute, attribute.Value, attribute.GetValueStartOffset (TextDocument));
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

			var begin = TextDocument.LocationToOffset (element.Region.End);
			int end;

			if (element.IsClosed && element.FirstChild == null) {
				end = TextDocument.LocationToOffset (element.ClosingTag.Region.Begin);
			} else {
				for (end = begin; end < (TextDocument.Length + 1) && TextDocument.GetCharAt (end) != '<'; end++) { }
			}
			var text = TextDocument.GetTextBetween (begin, end);

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