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

		protected virtual void VisitItemReference (XObject parent, string itemName)
		{
		}

		protected virtual void VisitPropertyReference (XObject parent, string propertyName)
		{
		}

		protected virtual void VisitMetadataReference (XObject parent, string itemName, string metadataName)
		{
		}

		void ExtractReferences (XElement element)
		{
			if (element.IsClosed && !element.IsSelfClosing) {
				var text = textDocument.GetTextBetween (element.Region.End, element.ClosingTag.Region.Begin);
				ExtractReferences (element, text);
			}
		}

		void ExtractReferences (XAttribute att)
		{
			if (!string.IsNullOrEmpty (att.Value))
				ExtractReferences (att, att.Value);
		}

		void ExtractReferences (XObject parent, string value)
		{
			try {
				var expr = new Expression ();
				//TODO: check options
				expr.Parse (value, ParseOptions.AllowItemsMetadataAndSplit);

				ExtractReferences (parent, expr);
			} catch (Exception ex) {
				LoggingService.LogError ($"Error parsing MSBuild expression at {parent.Region.Begin}", ex);
			}
		}

		void ExtractReferences (XObject parent, Expression expr)
		{
			foreach (var val in expr.Collection) {
				ExtractReferences (parent, val);
			}
		}

		void ExtractReferences (XObject parent, object val)
		{
			//TODO: InvalidExpressionError

			if (val is PropertyReference pr) {
				VisitPropertyReference (parent, pr.Name);
				return;
			}

			if (val is ItemReference ir) {
				if (ir.Transform != null)
					ExtractReferences (parent, ir.Transform);
				VisitItemReference (parent, ir.ItemName);
				return;
			}

			if (val is MetadataReference mr) {
				//TODO: unqualified metadata references
				VisitMetadataReference (parent, mr.ItemName, mr.MetadataName);
				return;
			}

			if (val is MemberInvocationReference mir) {
				ExtractReferences (parent, mir.Instance);
			}
		}
	}
}