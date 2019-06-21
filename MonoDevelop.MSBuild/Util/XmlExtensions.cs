// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Parser;

namespace MonoDevelop.MSBuild.Language
{
	public static class XmlExtensions
	{
		public static int GetValueOffset (this XAttribute att) => att.Span.End - att.Value.Length - 1;

		public static TextSpan GetValueSpan (this XAttribute att) => new Xml.Dom.TextSpan (att.Span.End - att.Value.Length - 1, att.Value.Length);

		//FIXME: this is fragile, need API in core
		public static char GetAttributeValueDelimiter (this XmlParser parser)
		{
			var ctx = (IXmlParserContext)parser;
			switch (ctx.StateTag) {
			case 3: return '"';
			case 2: return '\'';
			default: return (char)0;
			}
		}

		public static TextSpan GetNameSpan (this XElement element) => new TextSpan (element.Span.Start + 1, element.Name.FullName.Length);

		public static int GetNameOffset (this XElement element) => element.Span.Start + 1;

		public static TextSpan GetNameSpan (this XAttribute attribute) => new TextSpan (attribute.Span.Start, attribute.Name.FullName.Length);

		public static TextSpan GetSquiggleSpan (this XNode node)
		{
			return node is XElement el ? el.GetNameSpan () : node.NextSibling.Span;
		}

		public static XElement NextSiblingElement (this XElement element)
		{
			var node = element.NextSibling;
			while (node != null) {
				if (node is XElement nextElement) {
					return nextElement;
				}
				node = node.NextSibling;
			}
			return null;
		}

		public static IEnumerable<XElement> FollowingElements (this XElement element)
		{
			var node = element.NextSibling;
			while (node != null) {
				if (node is XElement nextElement) {
					yield return nextElement;
				}
				node = node.NextSibling;
			}
		}

		public static bool NameEquals (this INamedXObject obj, string name, bool ignoreCase)
		{
			var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
			return !obj.Name.HasPrefix && string.Equals (obj.Name.Name, name, comparison);
		}

		public static XElement ParentElement (this XElement element)
		{
			return (element.Parent as XElement);
		}

		//FIXME: binary search
		public static XObject FindNodeAtOffset (this XContainer container, int offset)
		{
			var node = container.AllDescendentNodes.FirstOrDefault (n => n.Span.Contains (offset));
			if (node != null) {
				if (node is IAttributedXObject attContainer) {
					var att = attContainer.Attributes.FirstOrDefault (n => n.Span.Contains (offset));
					if (att != null) {
						return att;
					}
				}
			}
			return node;
		}

		public static bool IsTrue (this XAttributeCollection attributes, string name)
		{
			var att = attributes.Get (new XName (name), true);
			return att != null && string.Equals (att.Value, "true", StringComparison.OrdinalIgnoreCase);
		}
	}
}
