// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using MonoDevelop.MSBuild.Language;

using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Parser;
using MonoDevelop.Xml.Tests;
using MonoDevelop.Xml.Tests.Utils;

namespace MonoDevelop.MSBuild.Tests;

partial class MSBuildDocumentTest
{
	public const char DefaultSelectionMarker = '|';

	public static IEnumerable<(int index, T result)> SelectAtMarkers<T> (
		string docString,
		Func<(XmlSpineParser parser, ITextSource textSource, MSBuildDocument doc, int offset), T> selector,
		string filename = null,
		char marker = DefaultSelectionMarker)
		=> SelectAtMarkers (TextWithMarkers.Parse (docString, marker), selector, filename);

	public static IEnumerable<(int index, T result)> SelectAtMarkers<T> (
		TextWithMarkers text,
		Func<(XmlSpineParser parser, ITextSource textSource, MSBuildDocument doc, int offset), T> selector,
		string filename = null,
		char? marker = null)
	{
		var indices = new Queue<int> (text.GetMarkedPositions (marker));

		var textDoc = new StringTextSource (text.Text);

		var treeParser = new XmlTreeParser (new XmlRootState ());
		var (xdoc, _) = treeParser.Parse (textDoc.CreateReader ());
		var logger = TestLoggerFactory.CreateTestMethodLogger ();
		var parseContext = new MSBuildParserContext (
			new NullMSBuildEnvironment (), null, null, null, filename ?? "test.csproj", new PropertyValueCollector (false), null, logger, null, default);
		var doc = CreateEmptyDocument ();
		doc.Build (xdoc, parseContext);

		var parser = new XmlSpineParser (treeParser.RootState);

		var nextIndex = indices.Dequeue ();
		for (int i = 0; i < textDoc.Length; i++) {
			parser.Push (textDoc[i]);
			if (parser.Position != nextIndex) {
				continue;
			}

			yield return (i, selector ((parser, textDoc, doc, i)));

			if (indices.Count == 0) {
				break;
			}
			nextIndex = indices.Dequeue ();
		}
	}

	public static TextSpan SpanFromLineColLength (string text, int line, int startCol, int length)
	{
		int currentLine = 1, currentCol = 1;
		for (int offset = 0; offset < text.Length; offset++) {
			if (currentLine == line && currentCol == startCol) {
				return new TextSpan (offset, length);
			}
			char c = text[offset];
			switch (c) {
			case '\r':
				if (offset + 1 < text.Length && text[offset + 1] == '\n') {
					offset++;
				}
				goto case '\n';
			case '\n':
				if (currentLine == line) {
					throw new ArgumentOutOfRangeException ($"Line {currentLine} ended at col {currentCol}");
				}
				currentLine++;
				currentCol = 1;
				break;
			default:
				currentCol++;
				break;
			}
		}
		throw new ArgumentOutOfRangeException ($"Reached line {currentLine}");
	}

	internal static MSBuildDocument CreateEmptyDocument ()
	{
		return new MSBuildDocument (null, false);
	}

}
