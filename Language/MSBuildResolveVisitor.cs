// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using MonoDevelop.Ide.Editor;
using MonoDevelop.MSBuildEditor.Schema;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Parser;

namespace MonoDevelop.MSBuildEditor.Language
{
	static class MSBuildResolver
	{
		public static MSBuildResolveResult Resolve (XmlParser parser, IReadonlyTextDocument document)
		{
			int offset = parser.Position;

			//clones and connects nodes to their parents
			parser = parser.GetTreeParser ();

			var nodePath = parser.Nodes.ToList ();
			nodePath.Reverse ();

			int i = offset;
			while (i < document.Length && (parser.CurrentState is XmlNameState || parser.CurrentState is XmlAttributeState || parser.CurrentState is XmlAttributeValueState)) {
				parser.Push (document.GetCharAt (i++));
			}

			//need to look up element by walking how the path, since at each level, if the parent has special children,
			//then that gives us information to identify the type of its children
			MSBuildLanguageElement languageElement = null;
			XElement el = null;
			XAttribute att = null;

			foreach (var node in nodePath) {
				if (node is XAttribute xatt && xatt.Name.Prefix == null) {
					att = xatt;
					break;
				}

				//if children of parent is known to be arbitrary data, don't go into it
				if (languageElement != null && languageElement.ValueKind == MSBuildValueKind.Data) {
					break;
				}
				
				//code completion is forgiving, all we care about best guess resolve for deepest child
				if (node is XElement xel && xel.Name.Prefix == null) {
					el = xel;
					languageElement = MSBuildLanguageElement.Get (el.Name.Name, languageElement);
					if (languageElement != null)
						continue;
				}

				languageElement = null;
			}

			if (languageElement == null) {
				return null;
			}

			var rr = new MSBuildResolveResult {
				LanguageElement = languageElement,
				XElement = el,
				XAttribute = att
			};

			var rv = new MSBuildResolveVisitor (offset, rr);
			rv.Run (el, languageElement, document);

			return rr;
		}

		class MSBuildResolveVisitor : MSBuildVisitor
		{
			int offset;
			readonly MSBuildResolveResult rr;

			public MSBuildResolveVisitor (int offset, MSBuildResolveResult rr)
			{
				this.offset = offset;
				this.rr = rr;
			}

			bool IsIn (int start, int length) => offset >= start && offset <= (start + length);

			public void Run (XElement el, MSBuildLanguageElement schemaEl, IReadonlyTextDocument textDoc)
			{
				SetTextDocument (textDoc.FileName, textDoc);
				VisitResolvedElement (el, schemaEl);
			}

			protected override void VisitResolvedElement (XElement element, MSBuildLanguageElement resolved)
			{
				var start = ConvertLocation (element.Region.Begin);
				bool inName = IsIn (start, element.Name.Name.Length);
				if (inName) {
					if (!resolved.IsAbstract) {
						rr.ReferenceKind = MSBuildReferenceKind.Keyword;
						rr.ReferenceOffset = start;
						rr.ReferenceName = element.Name.Name;
						return;
					}
					if (resolved.Kind == MSBuildKind.Task) {
						rr.ReferenceKind = MSBuildReferenceKind.Task;
						rr.ReferenceOffset = start;
						rr.ReferenceName = element.Name.Name;
						return;
					}
				}

				foreach (var att in element.Attributes) {
					var attStart = ConvertLocation (att.Region.Begin);
					if (IsIn (attStart, att.Name.Name.Length)) {
						var rat = resolved.GetAttribute (att.Name.Name);
						if (!rat.IsAbstract) {
							rr.ReferenceKind = MSBuildReferenceKind.Keyword;
							rr.ReferenceOffset = attStart;
							rr.ReferenceName = att.Name.Name;
							return;
						}
					}
				}

				base.VisitResolvedElement (element, resolved);
			}

			protected override void VisitItemReference (string itemName, int start, int length)
			{
				if (IsIn (start, length)) {
					rr.ReferenceKind = MSBuildReferenceKind.Item;
					rr.ReferenceOffset = start;
					rr.ReferenceName = itemName;
					base.VisitItemReference (itemName, start, length);
				}
			}

			protected override void VisitPropertyReference (string propertyName, int start, int length)
			{
				if (IsIn (start, length)) {
					rr.ReferenceKind = MSBuildReferenceKind.Property;
					rr.ReferenceOffset = start;
					rr.ReferenceName = propertyName;
					base.VisitPropertyReference (propertyName, start, length);
				}
			}

			protected override void VisitMetadataReference (string itemName, string metadataName, int start, int length)
			{
				if (IsIn (start, length)) {
					rr.ReferenceKind = MSBuildReferenceKind.Metadata;
					rr.ReferenceOffset = start;
					rr.ReferenceName = metadataName;
					rr.ReferenceItemName = itemName;
					base.VisitMetadataReference (itemName, metadataName, start, length);
				}
			}
		}
	}

	class MSBuildResolveResult
	{
		public XElement XElement;
		public XAttribute XAttribute;

		public MSBuildLanguageElement LanguageElement;

		public string AttributeName => XAttribute?.Name.Name;
		public string ElementName => XElement?.Name.Name;
		public string ParentName => (XElement?.Parent as XElement)?.Name.Name;

		public MSBuildReferenceKind ReferenceKind;
		public int ReferenceOffset;
		public string ReferenceName;
		public string ReferenceItemName;

	}

	enum MSBuildReferenceKind
	{
		None,
		Item,
		Property,
		Metadata,
		Task,
		TaskParameter,
		Keyword,
		Target
	}
}
