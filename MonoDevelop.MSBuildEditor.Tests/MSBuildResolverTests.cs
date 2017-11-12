//
// MSBuildResolverTests.cs
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

using System.Collections.Generic;
using System.Linq;
using MonoDevelop.Ide;
using MonoDevelop.MSBuildEditor.Language;
using NUnit.Framework;

namespace MonoDevelop.MSBuildEditor.Tests
{
	[TestFixture]
	public class MSBuildResolverTests : IdeTestBase
	{
		List<(int offset, MSBuildResolveResult result)> Resolve (string doc) => MSBuildTestHelpers
			.SelectAtMarkers (doc, "hello.csproj", (ctx) => MSBuildResolver.Resolve (ctx.parser, ctx.document))
			.ToList ();

		void AssertReferences (string doc, params (MSBuildKind kind, string name, string itemName)[] expected)
		{
			var results = Resolve (doc);

			if (results.Count != expected.Length) {
				Dump ();
				Assert.Fail ($"Expected {expected.Length} items, got {results.Count}");
			}

			for (int i = 0; i < results.Count; i++) {
				var a = results [i];
				var e = expected [i];
				if (a.result.ReferenceKind != e.kind || !string.Equals (a.result.ReferenceName, e.name) || !string.Equals (a.result.ReferenceItemName, e.itemName)) {
					Dump ();
					Assert.Fail ($"Index {i}: Expected '{e.kind}'='{FormatNameT (e)}', got '{a.result.ReferenceKind}'='{FormatNameRR (a.result)}'");
				}
			}

			void Dump ()
			{
				foreach (var r in results) {
					System.Console.WriteLine ($"{r.result.ReferenceKind}={FormatNameRR (r.result)} @{r.offset}");
				}
			}

			string FormatName (string name, string itemName) => itemName == null ? name : itemName + '.' + name;
			string FormatNameRR (MSBuildResolveResult rr) => FormatName (rr.ReferenceName, rr.ReferenceItemName);
			string FormatNameT ((MSBuildKind kind, string name, string itemName) rr) => FormatName (rr.name, rr.itemName);
		}

		[Test]
		public void ItemResolution()
		{
			var doc = @"
<project>
  <itemgroup>
    <fo|o />
    <bar include='@(|foo)' condition=""'@(Foo)'!='$(Foo)'"" somemetadata=""@(Foo)"" foo='a' />
    <!-- check that the metadata doesn't get flagged -->
    <bar><foo>@(f|oo)</foo></bar>
    <baz include=""@(fo|o->'%(fo|o.bar)')"" />
  </itemgroup>
</project>".TrimStart ();

			AssertReferences (
				doc,
				(MSBuildKind.ItemReference, "foo", null),
				(MSBuildKind.ItemReference, "foo", null),
				(MSBuildKind.ItemReference, "foo", null),
				(MSBuildKind.ItemReference, "foo", null),
				(MSBuildKind.ItemReference, "foo", null)
			);
		}

		[Test]
		public void PropertyResolution ()
		{
			var doc = @"
<project>
  <propertygroup>
    <f|oo condition=""'x$(F|oo)'==''"">bar $(fo|o)</foo>
  </propertygroup>
  <target name='Foo' DependsOnTargets='$(F|oo)'>
    <itemgroup>
      <foo />
      <bar include='$(foo)' condition=""'@(Foo)'!='$(Foo)'"" somemetadata=""$(Foo)"" foo='a' />
      <bar><foo>$(fo|o)</foo></bar>
      <foo include=""@(bar->'%(baz.foo)$(f|oo)')"" />
    </itemgroup>
  </target>
</project>".TrimStart ();

			AssertReferences (
				doc,
				(MSBuildKind.PropertyReference, "foo", null),
				(MSBuildKind.PropertyReference, "Foo", null),
				(MSBuildKind.PropertyReference, "foo", null),
				(MSBuildKind.PropertyReference, "Foo", null),
				(MSBuildKind.PropertyReference, "foo", null),
				(MSBuildKind.PropertyReference, "foo", null)
			);
		}

		[Test]
		public void MetadataResolution ()
		{
			var doc = @"
<project>
  <itemgroup>
    <bar fo|o=""$(foo)"" />
    <bar>
        <f|oo>baz</foo>
    </bar>
    <foo>
        <foo>baz</foo>
    </foo>
    <foo include=""@(bar->'%(foo)')"" foo='a' />
    <foo include=""@(bar->'%(bar.fo|o)')"" />
    <foo include=""@(bar->'%(baz.foo)')"" />
    <bar><foo>@(foo)</foo></bar>
  </itemgroup>
  <target name='Foo' DependsOnTargets=""@(bar->'%(F|oo)')"">
  </target>
</project>".TrimStart ();

			AssertReferences (
				doc,
				(MSBuildKind.MetadataReference, "foo", "bar"),
				(MSBuildKind.MetadataReference, "foo", "bar"),
				(MSBuildKind.MetadataReference, "foo", "bar"),
				(MSBuildKind.MetadataReference, "Foo", "bar")
			);
		}
	}
}
