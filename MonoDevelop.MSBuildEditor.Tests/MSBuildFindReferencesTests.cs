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
			collector.Run (filename, textDoc, xdoc);
			return collector.Results;
		}

		void AssertLocations (string doc, string expectedName, List<(int Offset, int Length)> actual, params (int Offset, int Length)[] expected)
		{
			if (actual.Count != expected.Length) {
				DumpLocations ();
				Assert.Fail ($"List has {actual.Count} items, expected {expected.Length}");
			}

			for (int i = 0; i < actual.Count; i++) {
				var a = actual [i];
				var e = expected [i];
				if (a.Offset != e.Offset || a.Length != e.Length || !string.Equals (expectedName, doc.Substring (a.Offset, a.Length), StringComparison.OrdinalIgnoreCase)) {
					DumpLocations ();
					Assert.Fail ($"Position {i}: expected ({e.Offset}, {e.Length})='{expectedName}', got ({a.Offset}, {a.Length})='{doc.Substring (a.Offset, a.Length)}'");
				}
			}

			void DumpLocations ()
			{
				Console.WriteLine ("Locations: ");
				foreach (var r in actual) {
					Console.WriteLine ($"    ({r.Offset}, {r.Length})='{doc.Substring (r.Offset, r.Length)}'");
				}
			}
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
    <baz include=""@(foo->'%(foo.bar)')"" />
  </itemgroup>
</project>".TrimStart ();

			var refs = FindReferences (doc, MSBuildKind.Item, "Foo", null);

			AssertLocations (
				doc, "foo", refs,
				(29, 3),
				(56, 3),
				(76, 3),
				(109, 3),
				(199, 3),
				(236, 3),
				(244, 3)
			);
		}

		[Test]
		public void FindPropertyReferences ()
		{
			var doc = @"
<project>
  <propertygroup>
    <foo condition=""'x$(Foo)'==''"">bar $(foo)</foo>
  </propertygroup>
  <target name='Foo' DependsOnTargets='$(Foo)'>
    <itemgroup>
      <foo />
      <bar include='$(foo)' condition=""'@(Foo)'!='$(Foo)'"" somemetadata=""$(Foo)"" foo='a' />
      <bar><foo>$(foo)</foo></bar>
      <foo include=""@(bar->'%(baz.foo)$(foo)')"" />
    </itemgroup>
  </target>
</project>".TrimStart ();

			var refs = FindReferences (doc, MSBuildKind.Property, "Foo", null);

			AssertLocations (
				doc, "foo", refs,
				(33, 3),
				(52, 3),
				(69, 3),
				(140, 3),
				(199, 3),
				(229, 3),
				(252, 3),
				(287, 3),
				(344, 3)
			);
		}

		[Test]
		public void FindMetadataReferences ()
		{
			var doc = @"
<project>
  <itemgroup>
    <bar foo=""$(foo)"" />
    <bar>
        <foo>baz</foo>
    </bar>
    <foo>
        <foo>baz</foo>
    </foo>
    <foo include=""@(bar->'%(foo)')"" foo='a' />
    <foo include=""@(bar->'%(bar.foo)')"" />
    <foo include=""@(bar->'%(baz.foo)')"" />
    <bar><foo>@(foo)</foo></bar>
  </itemgroup>
  <target name='Foo' DependsOnTargets=""@(bar->'%(Foo)')"">
  </target>
</project>".TrimStart ();

			var refs = FindReferences (doc, MSBuildKind.ItemMetadata, "foo", "bar");

			AssertLocations (
				doc, "foo", refs,
				(33, 3),
				(68, 3),
				(165, 3),
				(216, 3),
				(280, 3),
				(367, 3)
			);
		}
	}
}
