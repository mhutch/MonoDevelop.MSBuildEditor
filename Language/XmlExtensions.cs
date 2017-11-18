// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.Ide.Editor;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Parser;

namespace MonoDevelop.MSBuildEditor.Language
{
	public static class XmlExtensions
	{
		public static DocumentLocation GetValueStart (this XAttribute att, IReadonlyTextDocument doc)
		{
			int offset = doc.LocationToOffset (att.Region.End) - att.Value.Length - 1;
			return doc.OffsetToLocation(offset);
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
	}
}
