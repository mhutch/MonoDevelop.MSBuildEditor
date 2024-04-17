// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Microsoft.Extensions.Logging;

using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Language.Syntax;
using MonoDevelop.MSBuild.Language.Typesystem;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Parser;

namespace MonoDevelop.MSBuild.Language;

// FIXME: split this into two variants, one that operates on elements from an XDocument or spine parse, and one that operates on a MSBuildRootDocument.ProjectElement DOM
abstract class MSBuildDocumentVisitor
{
	protected MSBuildDocument Document { get; private set; }
	protected string? Filename => Document.Filename;
	protected ITextSource TextSource { get; private set; }
	protected ILogger Logger { get; private set; }
	protected CancellationToken CancellationToken { get; private set; }
	protected void CheckCancellation () => CancellationToken.ThrowIfCancellationRequested ();
	protected bool IsNotCancellation (Exception ex) => !(ex is OperationCanceledException && CancellationToken.IsCancellationRequested);

	protected MSBuildDocumentVisitor (MSBuildDocument document, ITextSource textSource, ILogger logger)
	{
		Document = document;
		TextSource = textSource;
		Logger = logger;
	}

	IEnumerable<IMSBuildSchema> GetSchemas () => Document.GetSchemas ();

	public void Run (XElement element, int offset = 0, int length = 0, CancellationToken token = default) => Run (element, null, offset, length, token);

	public void Run (XElement element, MSBuildElementSyntax? resolvedElement, int offset = 0, int length = 0, CancellationToken token = default)
	{
		CancellationToken = token;

		range = new TextSpan (offset, length > 0 ? length + offset : int.MaxValue);

		if (resolvedElement != null) {
			VisitResolvedElement (element, resolvedElement);
		} else if (element != null) {
			ResolveAndVisit (element, null);
		}
	}

	TextSpan range;

	void ResolveAndVisit (XElement element, MSBuildElementSyntax? parent)
	{
		CheckCancellation ();

		if (!element.Name.IsValid) {
			return;
		}
		var resolved = MSBuildElementSyntax.Get (element.Name.Name, parent);
		VisitResolvedElement (element, resolved);
	}

	void VisitResolvedElement (XElement element, MSBuildElementSyntax elementSyntax)
	{
		if (elementSyntax is not null) {
			// if this was resolved then elementName is not null
			string elementName = element.Name.Name!;
			var elementSymbol = GetSchemas ().GetElementInfo (elementSyntax, (element.Parent as XElement)?.Name.Name, elementName, true);
			VisitResolvedElement (element, elementSyntax, elementSymbol ?? elementSyntax);
		} else {
			VisitUnknownElement (element);
		}
	}

	protected virtual void VisitResolvedElement (XElement element, MSBuildElementSyntax elementSyntax, ITypedSymbol elementSymbol)
	{
		VisitResolvedElementChildren (element, elementSyntax, elementSymbol);
	}

	void VisitResolvedElementChildren (XElement element, MSBuildElementSyntax elementSyntax, ITypedSymbol elementSymbol)
	{
		ResolveAttributesAndValue (element, elementSyntax, elementSymbol);

		if (elementSyntax.ValueKind == MSBuildValueKind.Nothing) {
			foreach (var child in element.Elements) {
				if ((child.ClosingTag ?? child).Span.End < range.Start) {
					continue;
				}
				if (child.Span.Start > range.End) {
					return;
				}
				ResolveAndVisit (child, elementSyntax);
			}
		}
	}

	void ResolveAttributesAndValue (XElement element, MSBuildElementSyntax elementSyntax, ITypedSymbol elementSymbol)
	{
		foreach (var att in element.Attributes) {
			if (att.Span.End < range.Start) {
				continue;
			}
			if (att.Span.Start > range.End) {
				return;
			}

			// if this was resolved then elementName is not null
			string elementName = element.Name.Name!;

			if (att.Name.Name is not string attributeName) {
				continue;
			}

			var attributeSyntax = elementSyntax.GetAttribute (attributeName);

			if (attributeSyntax is null) {
				VisitUnknownAttribute (element, att);
				continue;
			}

			var attributeSymbol = GetSchemas ().GetAttributeInfo (attributeSyntax, elementName, attributeName);

			// GetAttributeInfo may have returned a specialized variant of the MSBuildAttributeSyntax, so update it
			if (attributeSymbol is MSBuildAttributeSyntax specializedAttributeSyntax) {
				attributeSyntax = specializedAttributeSyntax;
			}

			VisitResolvedAttribute (element, att, elementSyntax, attributeSyntax, elementSymbol, attributeSymbol ?? attributeSyntax);
		}

		if (elementSyntax.ValueKind != MSBuildValueKind.Nothing && elementSyntax.ValueKind != MSBuildValueKind.Data) {
			VisitElementValue (element, elementSyntax, elementSymbol);
			return;
		}
	}

	protected virtual void VisitResolvedAttribute (
		XElement element, XAttribute attribute,
		MSBuildElementSyntax elementSyntax, MSBuildAttributeSyntax attributeSyntax, ITypedSymbol elementSymbol, ITypedSymbol attributeSymbol)
	{
		VisitResolvedAttributeChildren (element, attribute, elementSyntax, attributeSyntax, elementSymbol, attributeSymbol);
	}

	void VisitResolvedAttributeChildren (
		XElement element, XAttribute attribute,
		MSBuildElementSyntax elementSyntax, MSBuildAttributeSyntax attributeSyntax, ITypedSymbol elementSymbol, ITypedSymbol attributeSymbol)
	{
		if (attribute.Value is string expressionText) {
			var expression = ParseValue (expressionText, attribute.ValueOffset, attributeSymbol);
			VisitAttributeValue (element, attribute, elementSyntax, attributeSyntax, elementSymbol, attributeSymbol, expressionText, expression);
		}
	}

	protected virtual void VisitUnknownElement (XElement element)
	{
	}

	protected virtual void VisitUnknownAttribute (XElement element, XAttribute attribute)
	{
	}

	void VisitElementValue (XElement element, MSBuildElementSyntax elementSyntax, ITypedSymbol elementSymbol)
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
			var expression = ParseValue (value, begin, elementSymbol);
			VisitElementValue (element, elementSyntax, elementSymbol, value, expression);
		}
	}

	protected virtual ExpressionOptions GetExpressionParseOptions (MSBuildValueKind inferredKind) => inferredKind.GetExpressionOptions ();

	ExpressionNode ParseValue (string value, int offset, ITypedSymbol valueSymbol)
	{
		// parse even if the kind disallows expressions, as this handles lists, whitespace, offsets, etc
		return valueSymbol.ValueKind == MSBuildValueKind.Condition
			? ExpressionParser.ParseCondition (value, offset)
			: ExpressionParser.Parse (value, GetExpressionParseOptions (valueSymbol.ValueKind), offset);
	}

	protected virtual void VisitElementValue (XElement element, MSBuildElementSyntax elementSyntax, ITypedSymbol elementSymbol, string expressionText, ExpressionNode expression)
	{
		VisitValue (element, null, elementSyntax, null, elementSymbol, null, expressionText, expression);
	}

	protected virtual void VisitAttributeValue (XElement element, XAttribute attribute, MSBuildElementSyntax elementSyntax, MSBuildAttributeSyntax attributeSyntax, ITypedSymbol elementSymbol, ITypedSymbol attributeSymbol, string expressionText, ExpressionNode expression)
	{
		VisitValue (element, attribute, elementSyntax, attributeSyntax, elementSymbol, attributeSymbol, expressionText, expression);
	}

	protected virtual void VisitValue (XElement element, XAttribute? attribute, MSBuildElementSyntax elementSyntax, MSBuildAttributeSyntax? attributeSyntax, ITypedSymbol elementSymbol, ITypedSymbol? attributeSymbol, string expressionText, ExpressionNode node)
	{
	}
}