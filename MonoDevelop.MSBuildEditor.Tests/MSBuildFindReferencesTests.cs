//
// MSBuildFindReferencesTests.cs
//
// Author:
//       Mikayla Hutchinson <m.j.hutchinson@gmail.com>
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
using System.IO;
using MonoDevelop.Core.Text;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Editor;
using MonoDevelop.Xml.Parser;
using NUnit.Framework;

namespace MonoDevelop.MSBuildEditor.Tests
{
	[TestFixture]
	public class MSBuildFindReferencesTests : IdeTestBase
	{
		List<(int Offset, int Length)> FindReferences (string doc, MSBuildKind kind, string name, string parentName)
		{
			string filename = "test.csproj";

			var textDoc = TextEditorFactory.CreateNewDocument (new StringTextSource (doc), filename, MSBuildTextEditorExtension.MSBuildMimeType);

			var xmlParser = new XmlParser (new XmlRootState (), true);
			xmlParser.Parse (new StringReader (doc));
			var xdoc = xmlParser.Nodes.GetRoot ();

			var collector = MSBuildReferenceCollector.Create (kind, name, parentName);
			collector.Run (filename, xdoc, textDoc);
			return collector.Results;
		}

		[Test]
		public void FindItemReferences ()
		{
			var doc = @"
<project>
  <itemgroup>
    <foo />
    <bar include='@(foo)' condition=""'@(Foo)'!='$(Foo)'"" somemetadata=""@(Foo)"" foo='a' />
    <!-- check that the metadata doesn't get flagged -->
	<bar><foo>@(foo)</foo></bar>
  </itemgroup>
</project>".TrimStart ();

			var refs = FindReferences (doc, MSBuildKind.Item, "Foo", null);

			Assert.AreEqual (5, refs.Count);

			AssertLocation (28, 7, refs [0]);
			AssertLocation (54, 6, refs [1]);
			AssertLocation (107, 6, refs [2]);
			AssertLocation (74, 6, refs [3]);
			AssertLocation (194, 6, refs [4]);
		}

		void AssertLocation (int expectedOffset, int expectedLength, (int offset, int length) span)
		{
			Assert.AreEqual (expectedOffset, span.offset);
			Assert.AreEqual (expectedLength, span.length);
		}
	}
}
