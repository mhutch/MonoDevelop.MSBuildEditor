// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using MonoDevelop.Ide.Editor;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Parser;

namespace MonoDevelop.MSBuildEditor.Language
{
	public static class XmlExtensions
	{
		public static int GetValueStartOffset (this XAttribute att, IReadonlyTextDocument doc)
		{
			return doc.LocationToOffset (att.Region.End) - att.Value.Length - 1;
		}

		public static DocumentLocation GetValueStart (this XAttribute att, IReadonlyTextDocument doc)
		{
			return doc.OffsetToLocation (GetValueStartOffset (att, doc));
		}

		public static DocumentRegion GetValueRegion (this XAttribute att, IReadonlyTextDocument doc)
		{
			int endOffset = doc.LocationToOffset (att.Region.End) - 1;
			int startOffset = endOffset - att.Value.Length;
			return new DocumentRegion (doc.OffsetToLocation (startOffset), doc.OffsetToLocation (endOffset));
		}

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

		public static DocumentRegion GetNameRegion (this XElement element)
		{
			var r = element.Region;
			return new DocumentRegion (r.BeginLine, r.BeginColumn + 1, r.BeginLine, r.BeginColumn + 1 + element.Name.FullName.Length);
		}

		public static DocumentRegion GetNameRegion (this XAttribute attribute)
		{
			var r = attribute.Region;
			return new DocumentRegion (r.BeginLine, r.BeginColumn, r.BeginLine, r.BeginColumn + attribute.Name.FullName.Length);
		}

		public static DocumentRegion GetSquiggleRegion (this XNode node)
		{
			return node is XElement el ? el.GetNameRegion () : node.NextSibling.Region;
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
	}
}
