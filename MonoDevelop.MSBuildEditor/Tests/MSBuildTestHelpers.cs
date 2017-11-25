//
// Copyright (c) 2017 Microsoft Corp.
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
using System.Collections.Generic;
using System.Text;
using MonoDevelop.Core.Text;
using MonoDevelop.Ide.Editor;
using MonoDevelop.MSBuildEditor.Language;
using MonoDevelop.Xml.Parser;

namespace MonoDevelop.MSBuildEditor.Tests
{
	static class MSBuildTestHelpers
	{
		const char defaultMarker = '|';

		public static List<int> GetMarkedIndices (ref string doc, char marker = defaultMarker)
		{
			var indices = new List<int> ();
			var docBuilder = new StringBuilder ();
			for (int i = 0; i < doc.Length; i++) {
				var ch = doc [i];
				if (ch == marker) {
					indices.Add (i - indices.Count);
				} else {
					docBuilder.Append (ch);
				}
			}
			doc = docBuilder.ToString ();
			return indices;
		}

		public static IEnumerable<(int index, T result)> SelectAtMarkers<T> (
			string doc, string filename,
			Func<(XmlParser parser, IReadonlyTextDocument document, MSBuildResolveContext ctx, int offset), T> selector,
			char marker = defaultMarker)
		{
			var indices = new Queue<int> (GetMarkedIndices (ref doc, marker));

			var textDoc = TextEditorFactory.CreateNewDocument (new StringTextSource (doc), filename, MSBuildTextEditorExtension.MSBuildMimeType);

			var treeParser = new XmlParser (new XmlRootState (), true);
			treeParser.Parse (textDoc.CreateReader ());
			var sb = new MSBuildSchemaBuilder (true, null, new PropertyValueCollector (false), null);
			var ctx = CreateEmptyContext ();
			sb.Run (treeParser.Nodes.GetRoot (), filename, textDoc, ctx);

			var parser = new XmlParser (new XmlRootState (), false);

			var nextIndex = indices.Dequeue ();
			for (int i = 0; i < doc.Length; i++) {
				parser.Push (doc [i]);
				if (i != nextIndex) {
					continue;
				}

				yield return (i, selector ((parser, textDoc, ctx, i)));

				if (indices.Count == 0) {
					break;
				}
				nextIndex = indices.Dequeue ();
			}
		}

		internal static MSBuildResolveContext CreateEmptyContext ()
		{
			return MSBuildResolveContext.Create (null, true, new Xml.Dom.XDocument (), null, null, null, null);
		}
    }
}
