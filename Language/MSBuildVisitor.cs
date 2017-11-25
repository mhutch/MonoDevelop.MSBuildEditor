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
		protected MSBuildResolveContext Context { get; private set; }
		protected string Filename { get; private set; }
		protected IReadonlyTextDocument Document { get; private set; }

		protected int ConvertLocation (DocumentLocation location) => Document.LocationToOffset (location);

		public void Run (XDocument xDocument, string filename, ITextSource document, MSBuildResolveContext context)
		{
			Run (xDocument.RootElement, null, filename, document, context);

		}

		public void Run (XElement element, MSBuildLanguageElement resolvedElement, string filename, ITextSource document, MSBuildResolveContext context)
		{
			Filename = filename;
			Context = context;

			//HACK: we should really use the ITextSource directly, but since the XML parser positions are
			//currently line/col, we need a TextDocument to convert to offsets
			Document = document as IReadonlyTextDocument
				?? TextEditorFactory.CreateNewReadonlyDocument (
					document, filename, MSBuildTextEditorExtension.MSBuildMimeType
				);

			if (resolvedElement != null) {
				VisitResolvedElement (element, resolvedElement);
			}else {
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
				VisitAttributeValue (element, attribute, resolvedAttribute, attribute.Value, attribute.GetValueStartOffset (Document));
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

			var begin = Document.LocationToOffset (element.Region.End);
			int end;

			if (element.IsClosed && element.FirstChild == null) {
				end = Document.LocationToOffset (element.ClosingTag.Region.Begin);
			} else {
				for (end = begin; end < (Document.Length + 1) && Document.GetCharAt (end) != '<'; end++) { }
			}
			var text = Document.GetTextBetween (begin, end);

			VisitElementValue (element, resolved, text, begin);
		}

		protected virtual void VisitElementValue (XElement element, MSBuildLanguageElement resolved, string value, int offset)
		{
		}

		protected virtual void VisitAttributeValue (XElement element, XAttribute attribute, MSBuildLanguageAttribute resolvedAttribute, string value, int offset)
		{
		}
	}
}