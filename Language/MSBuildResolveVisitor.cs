// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using MonoDevelop.Ide.Editor;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Parser;

namespace MonoDevelop.MSBuildEditor.Language
{
	static class MSBuildResolver
	{
		public static MSBuildResolveResult Resolve (XmlParser parser, IReadonlyTextDocument document)
		{
			int offset = parser.Position;

			parser = parser.GetTreeParser ();

			var nodePath = parser.Nodes.ToList ();
			nodePath.Reverse ();


			int i = offset;
			while (i < document.Length && (parser.CurrentState is XmlNameState || parser.CurrentState is XmlAttributeState || parser.CurrentState is XmlAttributeValueState)) {
				parser.Push (document.GetCharAt (i++));
			}

			//need to look up element by walking how the path, since at each level, if the parent has special children,
			//then that gives us information to identify the type of its children
			MSBuildLanguageElement schemaEl = null;
			string elName = null, attName = null, parentName = null;
			XElement el = null;
			XAttribute att = null;

			foreach (var node in nodePath) {
				if (node is XAttribute xatt && xatt.Name.Prefix == null) {
					attName = xatt.Name.Name;
					att = xatt;
					break;
				}

				//if children of parent is known to be arbitrary data, don't go into it
				if (schemaEl != null && schemaEl.ChildType == MSBuildKind.Data) {
					break;
				}
				
				//code completion is forgiving, all we care about best guess resolve for deepest child
				if (node is XElement xel && xel.Name.Prefix == null) {
					parentName = elName;
					el = xel;
					elName = xel.Name.Name;
					schemaEl = MSBuildLanguageElement.Get (elName, schemaEl);
					if (schemaEl != null)
						continue;
				}

				schemaEl = null;
				elName = null;
				parentName = null;
			}

			if (schemaEl == null) {
				return null;
			}

			var rr = new MSBuildResolveResult {
				AttributeName = attName,
				ElementName = elName,
				ParentName = parentName,
				LanguageElement = schemaEl,
				XElement = el,
				XAttribute = att
			};

			var rv = new MSBuildResolveVisitor (offset, rr);
			rv.Run (el, schemaEl, document);

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
				VisitResolved (el, schemaEl); 
			}

			protected override void VisitItemReference (string itemName, int start, int length)
			{
				if (IsIn (start, length)) {
					rr.ReferenceKind = MSBuildKind.ItemReference;
					rr.ReferenceName = itemName;
					base.VisitItemReference (itemName, start, length);
				}
			}

			protected override void VisitPropertyReference (string propertyName, int start, int length)
			{
				if (IsIn (start, length)) {
					rr.ReferenceKind = MSBuildKind.PropertyReference;
					rr.ReferenceName = propertyName;
					base.VisitPropertyReference (propertyName, start, length);
				}
			}

			protected override void VisitMetadataReference (string itemName, string metadataName, int start, int length)
			{
				if (IsIn (start, length)) {
					rr.ReferenceKind = MSBuildKind.MetadataReference;
					rr.ReferenceName = metadataName;
					rr.ReferenceItemName = itemName;
					base.VisitMetadataReference (itemName, metadataName, start, length);
				}
			}
		}
	}

	class MSBuildResolveResult
	{
		public MSBuildLanguageElement LanguageElement;
		public string AttributeName;
		public string ElementName;
		public string ParentName;
		public MSBuildKind? ReferenceKind;
		public string ReferenceName;
		public string ReferenceItemName;

		public XElement XElement;
		public XAttribute XAttribute;
	}
}
