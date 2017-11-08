//
// MSBuildVisitor.cs
//
// Author:
//       Mikayla Hutchinson <m.j.hutchinson@gmail.com>
//
// Copyright (c) 2016 Xamarin Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using MonoDevelop.Core;
using MonoDevelop.Core.Text;
using MonoDevelop.Ide.Editor;
using MonoDevelop.MSBuildEditor.ExpressionParser;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuildEditor
{
	abstract class MSBuildVisitor
	{
		IReadonlyTextDocument textDocument;

		protected void SetTextDocument (string fileName, ITextSource documentText)
		{
			//HACK: we should really use the ITextSource directly, but since the XML parser positions are
			//currently line/col, we need a TextDocument to convert to offsets
			textDocument = documentText as IReadonlyTextDocument
				?? TextEditorFactory.CreateNewReadonlyDocument (
					documentText, fileName, MSBuildTextEditorExtension.MSBuildMimeType
				);
		}

		protected int ConvertLocation (DocumentLocation location) => textDocument.LocationToOffset (location);

		public void Run (string fileName, ITextSource documentText, XDocument doc)
		{
			SetTextDocument (fileName, documentText);

			foreach (var el in doc.RootElement.Elements) {
				Run (el, null);
			}
		}

		 void Run (XElement el, MSBuildSchemaElement parent)
		{
			if (el.Name.Prefix != null) {
				return;
			}

			var resolved = MSBuildSchemaElement.Get (el.Name.FullName, parent);
			if (resolved == null) {
				VisitUnknown (el);
				return;
			}

			VisitResolved (el, resolved);
		}

		protected virtual void VisitResolved (XElement element, MSBuildSchemaElement resolved)
		{
			switch (resolved.Kind) {
			case MSBuildKind.Task:
				VisitTask (element);
				break;
			case MSBuildKind.Import:
				VisitImport (element);
				break;
			case MSBuildKind.Item:
				VisitItem (element);
				break;
			case MSBuildKind.Property:
				VisitProperty (element);
				break;
			case MSBuildKind.ItemMetadata:
				VisitMetadata (element, ((INamedXObject) element.Parent).Name.Name, element.Name.Name);
				break;
			case MSBuildKind.Target:
				VisitTarget (element);
				break;
			default:
				//other node types handle this explicitly to make sure reference ordering is correct
				ProcessCondition (element);
				break;
			}

			if (resolved.ChildType != MSBuildKind.Data) {
				foreach (var child in element.Elements) {
					Run (child, resolved);
				}
			}
		}

		void ProcessCondition (XElement element)
		{
			var condition = element.Attributes.Get (new XName ("Condition"), true);
			if (condition != null) {
				ExtractReferences (condition);
			}
		}

		protected virtual void VisitUnknown (XElement element)
		{
		}

		protected virtual void VisitTask (XElement element)
		{
			foreach (var att in element.Attributes) {
				if (!att.Name.HasPrefix) {
					VisitTaskParameter (att, element.Name.Name, att.Name.Name);
				}
			}
		}

		protected virtual void VisitTaskParameter (XAttribute attribute, string taskName, string parameterName)
		{
			ExtractReferences (attribute);
		}

		protected virtual void VisitItem (XElement element)
		{
			string itemName = element.Name.Name;
			VisitItemReference (itemName, ConvertLocation (element.Region.Begin) + 1, itemName.Length);

			foreach (var att in element.Attributes) {
				if (att.Name.HasPrefix) {
					continue;
				}
				switch (att.Name.Name.ToLowerInvariant ()) {
				case "include":
				case "exclude":
				case "remove":
				case "update":
				case "condition":
					ExtractReferences (att);
					continue;
				}
				VisitMetadataAttribute (att, element.Name.Name, att.Name.Name);
			}

			if (!element.IsSelfClosing && element.ClosingTag is XElement closing) {
				VisitItemReference (itemName, ConvertLocation (closing.Region.Begin) + 1, itemName.Length);
			}
		}

		protected virtual void VisitMetadata (XElement element, string itemName, string metadataName)
		{
			VisitMetadataReference (itemName, metadataName, ConvertLocation (element.Region.Begin) + 1, itemName.Length);

			ProcessCondition (element);

			ExtractReferences (element);

			if (!element.IsSelfClosing && element.ClosingTag is XElement closing) {
				VisitMetadataReference (itemName, metadataName, ConvertLocation (closing.Region.Begin) + 1, itemName.Length);
			}
		}

		protected virtual void VisitMetadataAttribute (XAttribute attribute, string itemName, string metadataName)
		{
			VisitMetadataReference (itemName, metadataName, ConvertLocation (attribute.Region.Begin), metadataName.Length);

			ExtractReferences (attribute);
		}

		protected virtual void VisitProperty (XElement element)
		{
			string propertyName = element.Name.Name;
			VisitPropertyReference (propertyName, ConvertLocation (element.Region.Begin) + 1, propertyName.Length);

			ProcessCondition (element);

			ExtractReferences (element);

			if (!element.IsSelfClosing && element.ClosingTag is XElement closing) {
				VisitPropertyReference (propertyName, ConvertLocation (closing.Region.Begin) + 1, propertyName.Length);
			}
		}

		protected virtual void VisitTarget (XElement element)
		{
			foreach (var att in element.Attributes) {
				switch (att.Name.Name.ToLowerInvariant ()) {
				case "dependsontargets":
				case "beforetargets":
				case "aftertargets":
				case "inputs":
				case "outputs":
				case "condition":
					ExtractReferences (att);
					break;
				}
			}
		}

		protected virtual void VisitImport (XElement element)
		{
			foreach (var att in element.Attributes) {
				switch (att.Name.Name.ToLowerInvariant ()) {
				case "project":
				case "condition:":
					ExtractReferences (att);
					break;
				}
			}
		}

		protected virtual void VisitItemReference (string itemName, int start, int length)
		{
		}

		protected virtual void VisitPropertyReference (string propertyName, int start, int length)
		{
		}

		protected virtual void VisitMetadataReference (string itemName, string metadataName, int start, int length)
		{
		}

		void ExtractReferences (XElement element)
		{
			if (element.IsSelfClosing) {
				return;
			}

			var begin = textDocument.LocationToOffset (element.Region.End);
			int end;

			if (element.IsClosed && element.FirstChild == null) {
				end = textDocument.LocationToOffset (element.ClosingTag.Region.Begin);
			} else {
				for (end = begin; end < textDocument.Length && textDocument.GetCharAt (end) != '<'; end++) {}
			}
			var text = textDocument.GetTextBetween (begin, end);
			ExtractReferences (text, begin);
		}

		void ExtractReferences (XAttribute att)
		{
			if (!string.IsNullOrEmpty (att.Value)) {
				//we don't know how much space there is around the = so work backwards from the end
				var end = textDocument.LocationToOffset (att.Region.End);
				var start = end - att.Value.Length - 1;
				ExtractReferences (att.Value, start);
			}
		}

		void ExtractReferences (string value, int startOffset)
		{
			try {
				var expr = new Expression ();
				//TODO: check options
				expr.Parse (value, ParseOptions.AllowItemsMetadataAndSplit);

				ExtractReferences (expr, startOffset);
			} catch (Exception ex) {
				LoggingService.LogError ($"Error parsing MSBuild expression at {startOffset}", ex);
			}
		}

		void ExtractReferences (Expression expr, int startOffset, ItemReference transformParent = null)
		{
			foreach (var val in expr.Collection) {
				ExtractReferences (val, startOffset, transformParent);
			}
		}

		void ExtractReferences (object val, int startOffset, ItemReference transformParent = null)
		{
			//TODO: InvalidExpressionError

			if (val is PropertyReference pr) {
				VisitPropertyReference (pr.Name, startOffset + pr.AbsoluteIndex, pr.Name.Length);
				return;
			}

			if (val is ItemReference ir) {
				VisitItemReference (ir.ItemName, startOffset + ir.AbsoluteIndex, ir.ItemName.Length);
				if (ir.Transform != null) {
					ExtractReferences (ir.Transform, startOffset, ir);
				}
				return;
			}

			if (val is MetadataReference mr) {
				//TODO: contextual metadata references e.g. batching
				var itemName = mr.ItemName;
				if (string.IsNullOrEmpty (itemName)) {
					itemName = transformParent?.ItemName;
				}
				if (mr.IsQualified) {
					VisitItemReference (itemName, startOffset + mr.AbsoluteIndex - mr.ItemName.Length - 1, mr.ItemName.Length);
					VisitMetadataReference (itemName, mr.MetadataName, startOffset + mr.AbsoluteIndex, mr.MetadataName.Length);
				} else {
					VisitMetadataReference (itemName, mr.MetadataName, startOffset + mr.AbsoluteIndex, mr.MetadataName.Length);
				}
				return;
			}

			if (val is MemberInvocationReference mir) {
				ExtractReferences (mir.Instance, startOffset);
				return;
			}

			if (val is Expression expr) {
				ExtractReferences (expr, startOffset, transformParent);
				return;
			}
		}
	}
}