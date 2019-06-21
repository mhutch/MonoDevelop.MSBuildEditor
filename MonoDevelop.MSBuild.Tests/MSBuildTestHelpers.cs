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
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Util;
using MonoDevelop.Xml.Parser;

namespace MonoDevelop.MSBuild.Tests
{
	static class MSBuildTestHelpers
	{
		const char defaultMarker = '|';

		public static List<int> GetMarkedIndices (ref string docString, char marker = defaultMarker)
		{
			var indices = new List<int> ();
			var docBuilder = new StringBuilder ();
			for (int i = 0; i < docString.Length; i++) {
				var ch = docString [i];
				if (ch == marker) {
					indices.Add (i - indices.Count);
				} else {
					docBuilder.Append (ch);
				}
			}
			docString = docBuilder.ToString ();
			return indices;
		}

		public static IEnumerable<(int index, T result)> SelectAtMarkers<T> (
			string docString, string filename,
			Func<(XmlParser parser, ITextSource textSource, MSBuildDocument doc, int offset), T> selector,
			char marker = defaultMarker)
		{
			var indices = new Queue<int> (GetMarkedIndices (ref docString, marker));

			var textDoc = TextSourceFactory.CreateNewDocument (docString, filename);

			var treeParser = new XmlParser (new XmlRootState (), true);
			treeParser.Parse (textDoc.CreateReader ());
			var sb = new MSBuildSchemaBuilder (true, null, new PropertyValueCollector (false), null, null);
			var doc = CreateEmptyDocument ();
			sb.Run (treeParser.Nodes.GetRoot (), filename, textDoc, doc);

			var parser = new XmlParser (new XmlRootState (), false);

			var nextIndex = indices.Dequeue ();
			for (int i = 0; i < docString.Length; i++) {
				parser.Push (docString [i]);
				if (i != nextIndex) {
					continue;
				}

				yield return (i, selector ((parser, textDoc, doc, i)));

				if (indices.Count == 0) {
					break;
				}
				nextIndex = indices.Dequeue ();
			}
		}

		internal static MSBuildDocument CreateEmptyDocument ()
		{
			return new MSBuildDocument (null, false);
		}

		static bool registeredAssemblies;

		public static void RegisterMSBuildAssemblies ()
		{
			if (registeredAssemblies) {
				return;
			}
			registeredAssemblies = true;

			if (Platform.IsWindows) {
				Microsoft.Build.Locator.MSBuildLocator.RegisterDefaults ();
			}
			else if (Platform.IsMac) {
				Microsoft.Build.Locator.MSBuildLocator.RegisterMSBuildPath (
					"/Library/Frameworks/Mono.framework/Versions/Current/lib/mono/msbuild/Current/bin"
				);
			}
			else {
				Microsoft.Build.Locator.MSBuildLocator.RegisterMSBuildPath (
					"/usr/lib/mono/msbuild/Current/bin"
				);
			}
		}
	}
}
