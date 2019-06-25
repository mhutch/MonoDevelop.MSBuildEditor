// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Language
{
	//FIXME: push more of this down into MonoDevelop.Xml
	public static class XmlExtensions
	{
		public static TextSpan GetSquiggleSpan (this XNode node)
		{
			return node is XElement el ? el.NameSpan : node.NextSibling.Span;
		}

		public static bool NameEquals (this INamedXObject obj, string name, bool ignoreCase)
		{
			var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
			return !obj.Name.HasPrefix && string.Equals (obj.Name.Name, name, comparison);
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
