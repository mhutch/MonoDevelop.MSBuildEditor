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

		public void Run (string fileName, XDocument doc, ITextSource documentText)
		{
			//HACK: we should really use the ITextSource directly, but since the XML parser positions are
			//currently line/col, we need a TextDocument to convert to offsets
			textDocument = TextEditorFactory.CreateNewReadonlyDocument (
				documentText, fileName, MSBuildTextEditorExtension.MSBuildMimeType
			);

			foreach (var el in doc.RootElement.Elements) {
				Traverse (el, null);
			}
		}

		protected int ConvertLocation (DocumentLocation location) => textDocument.LocationToOffset (location);

		void Traverse (XElement el, MSBuildElement parent)
		{
			if (el.Name.Prefix != null) {
				return;
			}

			var resolved = MSBuildElement.Get (el.Name.FullName, parent);
			if (resolved == null) {
				VisitUnknown (el);
				return;
			}

			VisitResolved (el, resolved);
		}

		protected virtual void VisitResolved (XElement element, MSBuildElement resolved)
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
			}

			foreach (var child in element.Elements) {
				Traverse (child, resolved);
			}

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
			foreach (var att in element.Attributes) {
				if (att.Name.HasPrefix) {
					continue;
				}
				switch (att.Name.Name.ToLowerInvariant ()) {
				case "include":
				case "exclude":
				case "remove":
				case "update":
					ExtractReferences (att);
					continue;
				case "condition":
					//already handled in VisitResolved
					continue;
				}
				VisitMetadataAttribute (att, element.Name.Name, att.Name.Name);
			}
		}

		protected virtual void VisitMetadata (XElement element, string itemName, string metadataName)
		{
			ExtractReferences (element);
		}

		protected virtual void VisitMetadataAttribute (XAttribute attribute, string itemName, string metadataName)
		{
			ExtractReferences (attribute);
		}

		protected virtual void VisitProperty (XElement element)
		{
			ExtractReferences (element);
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
					ExtractReferences (att);
					break;
				}
			}
		}

		protected virtual void VisitImport (XElement element)
		{
			foreach (var att in element.Attributes) {
				if (string.Equals (att.Name.Name, "Project", StringComparison.OrdinalIgnoreCase)) {
					ExtractReferences (att);
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
			if (element.IsClosed && !element.IsSelfClosing) {
				var begin = textDocument.LocationToOffset (element.Region.End);
				var end = textDocument.LocationToOffset (element.ClosingTag.Region.Begin);
				var text = textDocument.GetTextBetween (begin, end);
				ExtractReferences (text, begin);
			}
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
				if (ir.Transform != null) {
					ExtractReferences (ir.Transform, startOffset, ir);
				}
				VisitItemReference (ir.ItemName, startOffset + ir.AbsoluteIndex, ir.ItemName.Length);
				return;
			}

			if (val is MetadataReference mr) {
				//TODO: contextual metadata references e.g. batching
				var itemName = mr.ItemName;
				if (string.IsNullOrEmpty (itemName)) {
					itemName = transformParent?.ItemName;
				}
				VisitMetadataReference (itemName, mr.MetadataName, startOffset + mr.AbsoluteIndex, mr.MetadataName.Length);
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