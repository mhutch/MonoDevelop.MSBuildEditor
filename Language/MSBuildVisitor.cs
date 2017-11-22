// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using MonoDevelop.Core;
using MonoDevelop.Core.Text;
using MonoDevelop.Ide.Editor;
using MonoDevelop.MSBuildEditor.ExpressionParser;
using MonoDevelop.MSBuildEditor.Schema;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuildEditor.Language
{
	abstract class MSBuildVisitor
	{
		protected IReadonlyTextDocument Document { get; private set; }

		protected void SetTextDocument (string fileName, ITextSource documentText)
		{
			//HACK: we should really use the ITextSource directly, but since the XML parser positions are
			//currently line/col, we need a TextDocument to convert to offsets
			Document = documentText as IReadonlyTextDocument
				?? TextEditorFactory.CreateNewReadonlyDocument (
					documentText, fileName, MSBuildTextEditorExtension.MSBuildMimeType
				);
		}

		protected int ConvertLocation (DocumentLocation location) => Document.LocationToOffset (location);

		public void Run (string fileName, ITextSource documentText, XDocument doc)
		{
			SetTextDocument (fileName, documentText);
			Run (doc.RootElement, null);
		}

		void Run (XElement el, MSBuildLanguageElement parent)
		{
			if (el.Name.Prefix == null) {
				var resolved = MSBuildLanguageElement.Get (el.Name.FullName, parent);
				if (resolved != null) {
					VisitResolvedElement (el, resolved);
					return;
				}
			}

			VisitUnknownElement (el);
		}

		protected virtual void VisitResolvedElement (XElement element, MSBuildLanguageElement resolved)
		{
			switch (resolved.Kind) {
			case MSBuildKind.Task:
				VisitTask (element, resolved);
				break;
			case MSBuildKind.Import:
				VisitImport (element, resolved);
				break;
			case MSBuildKind.Item:
				VisitItem (element, resolved);
				break;
			case MSBuildKind.Property:
				VisitProperty (element, resolved);
				break;
			case MSBuildKind.Metadata:
				VisitMetadata (element, resolved, ((INamedXObject) element.Parent).Name.Name, element.Name.Name);
				break;
			case MSBuildKind.Target:
				VisitTarget (element, resolved);
				break;
			default:
				//other node types handle this explicitly to make sure reference ordering is correct
				ResolveAttributes (element, resolved);
				break;
			}

			if (resolved.ValueKind == MSBuildValueKind.Nothing) {
				foreach (var child in element.Elements) {
					Run (child, resolved);
				}
			}
		}

		void ResolveAttributes (XElement element, MSBuildLanguageElement resolved)
		{
			foreach (var att in element.Attributes) {
				var resolvedAtt = resolved.GetAttribute (att.Name.FullName);
				if (resolvedAtt != null) {
					VisitResolvedAttribute (element, att, resolved, resolvedAtt);
					continue;
				}
				VisitUnknownAttribute (element, att);
			}
		}

		protected virtual void VisitResolvedAttribute (
			XElement element, XAttribute attribute,
			MSBuildLanguageElement resolvedElement, MSBuildLanguageAttribute resolvedAttribute)
		{
			if (resolvedAttribute.IsAbstract) {
				switch (resolvedAttribute.AbstractKind) {
				case MSBuildKind.Metadata:
					VisitMetadataAttribute (attribute, element.Name.Name, attribute.Name.Name);
					return;
				case MSBuildKind.TaskParameter:
					VisitTaskParameter (attribute, element.Name.Name, attribute.Name.Name);
					return;
				default:
					throw new Exception ($"Unhandled abstract attribute kind {resolvedAttribute.AbstractKind}");
				}
			}

			if (resolvedAttribute.ValueKind.AllowExpressions ()) {
				ExtractReferences (attribute);
			}
		}

		protected virtual void VisitUnknownElement (XElement element)
		{
		}

		protected virtual void VisitUnknownAttribute (XElement element, XAttribute attribute)
		{
		}

		protected virtual void VisitTask (XElement element, MSBuildLanguageElement resolved)
		{
			ResolveAttributes (element, resolved);
		}

		protected virtual void VisitTaskParameter (XAttribute attribute, string taskName, string parameterName)
		{
			ExtractReferences (attribute);
		}

		protected virtual void VisitItem (XElement element, MSBuildLanguageElement resolved)
		{
			string itemName = element.Name.Name;
			VisitItemReference (itemName, ConvertLocation (element.Region.Begin) + 1, itemName.Length);

			ResolveAttributes (element, resolved);

			if (!element.IsSelfClosing && element.ClosingTag is XElement closing) {
				VisitItemReference (itemName, ConvertLocation (closing.Region.Begin) + 1, itemName.Length);
			}
		}

		protected virtual void VisitMetadata (XElement element, MSBuildLanguageElement resolved, string itemName, string metadataName)
		{
			VisitMetadataReference (itemName, metadataName, ConvertLocation (element.Region.Begin) + 1, metadataName.Length);

			ResolveAttributes (element, resolved);

			ExtractReferences (element);

			if (!element.IsSelfClosing && element.ClosingTag is XElement closing) {
				VisitMetadataReference (itemName, metadataName, ConvertLocation (closing.Region.Begin) + 1, metadataName.Length);
			}
		}

		protected virtual void VisitMetadataAttribute (XAttribute attribute, string itemName, string metadataName)
		{
			VisitMetadataReference (itemName, metadataName, ConvertLocation (attribute.Region.Begin), metadataName.Length);

			ExtractReferences (attribute);
		}

		protected virtual void VisitProperty (XElement element, MSBuildLanguageElement resolved)
		{
			string propertyName = element.Name.Name;
			VisitPropertyReference (propertyName, ConvertLocation (element.Region.Begin) + 1, propertyName.Length);

			ResolveAttributes (element, resolved);

			ExtractReferences (element);

			if (!element.IsSelfClosing && element.ClosingTag is XElement closing) {
				VisitPropertyReference (propertyName, ConvertLocation (closing.Region.Begin) + 1, propertyName.Length);
			}
		}

		protected virtual void VisitTarget (XElement element, MSBuildLanguageElement resolved)
		{
			ResolveAttributes (element, resolved);
		}

		protected virtual void VisitImport (XElement element, MSBuildLanguageElement resolved)
		{
			ResolveAttributes (element, resolved);
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
			if (element.IsSelfClosing || !element.IsEnded) {
				return;
			}

			var begin = Document.LocationToOffset (element.Region.End);
			int end;

			if (element.IsClosed && element.FirstChild == null) {
				end = Document.LocationToOffset (element.ClosingTag.Region.Begin);
			} else {
				for (end = begin; end < (Document.Length + 1) && Document.GetCharAt (end) != '<'; end++) {}
			}
			var text = Document.GetTextBetween (begin, end);
			ExtractReferences (text, begin);
		}

		void ExtractReferences (XAttribute att)
		{
			if (!string.IsNullOrEmpty (att.Value)) {
				//we don't know how much space there is around the = so work backwards from the end
				var end = Document.LocationToOffset (att.Region.End);
				var start = end - att.Value.Length - 1;
				ExtractReferences (att.Value, start);
			}
		}

		void ExtractReferences (string value, int startOffset)
		{
			try {
				var expr = new Expression ();
				//TODO: check options. currently not splitting because it messes up offsets.
				expr.Parse (value, ParseOptions.AllowItems | ParseOptions.AllowMetadata);

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