// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.Linq;
using System.Threading;

using Microsoft.Extensions.Logging;

using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Parser;
using MonoDevelop.MSBuild.Language.Syntax;
using MonoDevelop.MSBuild.Language.Typesystem;

namespace MonoDevelop.MSBuild.Language
{
	abstract class MSBuildVisitor
	{
		protected MSBuildDocument Document { get; private set; }
		protected string Filename => Document.Filename;
		protected ITextSource TextSource { get; private set; }
		protected string Extension { get; private set; }
		protected ILogger Logger { get; private set; }
		protected CancellationToken CancellationToken { get; private set; }
		protected void CheckCancellation () => CancellationToken.ThrowIfCancellationRequested ();
		protected bool IsNotCancellation (Exception ex) => !(ex is OperationCanceledException && CancellationToken.IsCancellationRequested);

		protected bool IsTargetsFile => string.Equals (Extension, ".targets", StringComparison.OrdinalIgnoreCase);
		protected bool IsPropsFile => string.Equals (Extension, ".props", StringComparison.OrdinalIgnoreCase);

		protected MSBuildVisitor (MSBuildDocument document, ITextSource textSource, ILogger logger)
		{
			Document = document;
			Extension = document.Filename == null ? ".props" : System.IO.Path.GetExtension (Filename);
			TextSource = textSource;
			Logger = logger;
		}

		public void Run (XElement element, int offset = 0, int length = 0, CancellationToken token = default) => Run (element, null, offset, length, token);

		public void Run (XElement element, MSBuildElementSyntax? resolvedElement, int offset = 0, int length = 0, CancellationToken token = default)
		{
			CancellationToken = token;

			range = new TextSpan (offset, length > 0 ? length + offset : int.MaxValue);

			BeforeRun ();

			if (resolvedElement != null) {
				VisitResolvedElement (element, resolvedElement);
			} else if (element != null) {
				ResolveAndVisit (element, null);
			}
		}

		protected virtual void BeforeRun ()
		{
		}

		TextSpan range;

		void ResolveAndVisit (XElement element, MSBuildElementSyntax? parent)
		{
			CheckCancellation ();

			if (!element.Name.IsValid) {
				return;
			}
			var resolved = MSBuildElementSyntax.Get (element.Name.Name, parent);
			if (resolved != null) {
				VisitResolvedElement (element, resolved);
			} else {
				VisitUnknownElement (element);
			}
		}

		protected virtual void VisitResolvedElement (XElement element, MSBuildElementSyntax resolved)
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

		void ResolveAttributesAndValue (XElement element, MSBuildElementSyntax resolved)
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
			MSBuildElementSyntax resolvedElement, MSBuildAttributeSyntax resolvedAttribute)
		{
			if (attribute.Value != null) {
				VisitAttributeValue (element, attribute, resolvedElement, resolvedAttribute, attribute.Value, attribute.ValueOffset);
			}
		}

		protected virtual void VisitUnknownElement (XElement element)
		{
		}

		protected virtual void VisitUnknownAttribute (XElement element, XAttribute attribute)
		{
		}

		void VisitElementValue (XElement element, MSBuildElementSyntax resolved)
		{
			if (element.IsSelfClosing || !element.IsEnded) {
				return;
			}

			//FIXME: handle case with multiple text nodes with comments between them
			string value = string.Empty;
			var begin = element.Span.End;
			var textNode = element.Nodes.OfType<XText> ().FirstOrDefault ();
			if (textNode != null) {
				begin = textNode.Span.Start;
				value = TextSource.GetTextBetween (begin, textNode.Span.End);
			}

			VisitElementValue (element, resolved, value, begin);
		}

		protected virtual void VisitElementValue (XElement element, MSBuildElementSyntax resolved, string value, int offset)
		{
		}

		protected virtual void VisitAttributeValue (XElement element, XAttribute attribute, MSBuildElementSyntax resolvedElement, MSBuildAttributeSyntax resolvedAttribute, string value, int offset)
		{
		}
	}
}