// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.Core.Text;
using MonoDevelop.Ide.Editor;
using MonoDevelop.Ide.TypeSystem;
using MonoDevelop.MSBuildEditor.Schema;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuildEditor.Language
{
	abstract class MSBuildVisitor
	{
		protected MSBuildDocument Document { get; private set; }
		protected string Filename { get; private set; }
		protected IReadonlyTextDocument TextDocument { get; private set; }
		protected string Extension { get; private set; }

		protected bool IsTargetsFile => string.Equals (Extension, ".targets", System.StringComparison.OrdinalIgnoreCase);
		protected bool IsPropsFile => string.Equals (Extension, ".props", System.StringComparison.OrdinalIgnoreCase);

		protected int ConvertLocation (DocumentLocation location) => TextDocument.LocationToOffset (location);

		public void Run (MSBuildRootDocument doc, int offset = 0, int length = 0)
		{
			Run (doc.XDocument, doc.Filename, doc.Text, doc, offset, length);
		}

		public void Run (XDocument xDocument, string filename, ITextSource textDocument, MSBuildDocument doc, int offset = 0, int length = 0)
		{
			Run (xDocument.RootElement, null, filename, textDocument, doc, offset, length);
		}

		public void Run (XElement element, MSBuildLanguageElement resolvedElement, string filename, ITextSource textDocument, MSBuildDocument document, int offset = 0, int length = 0)
		{
			Filename = filename;
			Document = document;
			Extension = System.IO.Path.GetExtension (filename);

			//HACK: we should really use the ITextSource directly, but since the XML parser positions are
			//currently line/col, we need a TextDocument to convert to offsets
			TextDocument = textDocument as IReadonlyTextDocument
				?? TextEditorFactory.CreateNewReadonlyDocument (
					textDocument, filename, MSBuildTextEditorExtension.MSBuildMimeType
				);

			range = new DocumentRegion (
				TextDocument.OffsetToLocation (offset),
				length > 0
					? TextDocument.OffsetToLocation (length + offset)
					: new DocumentLocation (int.MaxValue, int.MaxValue));

			if (resolvedElement != null) {
				VisitResolvedElement (element, resolvedElement);
			} else if (element != null) {
				ResolveAndVisit (element, null);
			}
		}

		DocumentRegion range;

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
					if ((child.ClosingTag ?? child).Region.End < range.Begin) {
						continue;
					}
					if (child.Region.Begin > range.End) {
						return;
					}
					ResolveAndVisit (child, resolved);
				}
			}
		}

		void ResolveAttributesAndValue (XElement element, MSBuildLanguageElement resolved)
		{
			foreach (var att in element.Attributes) {
				if (att.Region.End < range.Begin) {
					continue;
				}
				if (att.Region.Begin > range.End) {
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
			int end = begin;

			if (element.IsClosed && element.FirstChild == null) {
				end = TextDocument.LocationToOffset (element.ClosingTag.Region.Begin);
			} else {
				//HACK: in some cases GetCharAt can throw at the end of the document even with TextDocument.Length check
				try {
					for (; end < (TextDocument.Length + 1) && TextDocument.GetCharAt (end) != '<'; end++) { }
				} catch {
					end--;
				}
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

		protected void AddError (ErrorType errorType, string message, DocumentRegion region) => Document.Errors.Add (new Error (errorType, message, region));
		protected void AddError (ErrorType errorType, string message, int offset, int length) => Document.Errors.Add (new Error (errorType, message, GetRegion (offset, length)));
		protected DocumentRegion GetRegion (int offset, int length) => new DocumentRegion (TextDocument.OffsetToLocation (offset), TextDocument.OffsetToLocation (offset + length));
		protected void AddError (string message, DocumentRegion region) => AddError (ErrorType.Error, message, region);
		protected void AddError (string message, int offset, int length) => AddError (ErrorType.Error, message, offset, length);
		protected void AddWarning (string message, DocumentRegion region) => AddError (ErrorType.Warning, message, region);
		protected void AddWarning (string message, int offset, int length) => AddError (ErrorType.Warning, message, offset, length);

	}
}