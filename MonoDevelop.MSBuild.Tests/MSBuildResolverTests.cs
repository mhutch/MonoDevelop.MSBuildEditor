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

using System;
using System.Collections.Generic;
using System.Linq;
using MonoDevelop.MSBuild.Editor.Roslyn;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Schema;
using NUnit.Framework;

namespace MonoDevelop.MSBuild.Tests
{
	[TestFixture]
	public class MSBuildResolverTests
	{
		List<(int offset, MSBuildResolveResult result)> Resolve (string doc)
		{
			return MSBuildTestHelpers
				.SelectAtMarkers (doc, "hello.csproj", (state) => MSBuildResolver.Resolve (state.parser, state.textSource, state.doc))
				.ToList ();
		}

		void AssertReferences (string doc, params (MSBuildReferenceKind kind, object reference)[] expected)
		{
			var results = Resolve (doc);

			if (results.Count != expected.Length) {
				Dump ();
				Assert.Fail ($"Expected {expected.Length} items, got {results.Count}");
			}

			for (int i = 0; i < results.Count; i++) {
				var a = results [i];
				var e = expected [i];
				if (a.result.ReferenceKind != e.kind || !IgnoreCaseEquals (e.kind, a.result.Reference, e.reference)) {
					Dump ();
					Assert.Fail ($"Index {i}: Expected '{e.kind}'='{FormatName (e.reference)}', got '{a.result.ReferenceKind}'='{FormatNameRR (a.result)}'");
				}
			}

			void Dump ()
			{
				foreach (var r in results) {
					Console.WriteLine ($"{r.result.ReferenceKind}={FormatNameRR (r.result)} @{r.offset}");
				}
			}

			string FormatName (object reference) {
				if (reference is ValueTuple<string, string> t) return $"{t.Item1}.{t.Item2}";
				if (reference is string s) return s;
				if (reference is MSBuildLanguageElement el) return $"Element/{el.Name}";
				if (reference is MSBuildLanguageAttribute att) return $"Attribute/{att.Name}";
				return reference?.ToString () ?? "[null]";
			}

			string FormatNameRR (MSBuildResolveResult rr) => FormatName (rr.Reference);
			bool IgnoreCaseEquals (MSBuildReferenceKind kind, object a, object e)
			{
				if (e is ValueTuple<string,string> et) {
					var at = (ValueTuple<string, string>)a;
					return string.Equals (at.Item1, et.Item1, StringComparison.OrdinalIgnoreCase) &&
						string.Equals (at.Item2, et.Item2, StringComparison.OrdinalIgnoreCase);
				}
				if (e is ValueTuple<MSBuildValueKind, string> vk) {
					var at = (ValueTuple<MSBuildValueKind, string>)a;
					return at.Item1 == vk.Item1 &&
						string.Equals (at.Item2, vk.Item2, StringComparison.OrdinalIgnoreCase);
				}
				if (kind == MSBuildReferenceKind.Keyword) {
					if (a is MSBuildLanguageElement el) {
						a = el.Name;
					} else if (a is MSBuildLanguageAttribute att) {
						a = att.Name;
					}
				}
				return string.Equals ((string)a, (string)e, StringComparison.OrdinalIgnoreCase);
			}
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
				(MSBuildReferenceKind.Item, "foo"),
				(MSBuildReferenceKind.Item, "foo"),
				(MSBuildReferenceKind.Item, "foo"),
				(MSBuildReferenceKind.Item, "foo"),
				(MSBuildReferenceKind.Item, "foo")
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
				(MSBuildReferenceKind.Property, "foo"),
				(MSBuildReferenceKind.Property, "Foo"),
				(MSBuildReferenceKind.Property, "foo"),
				(MSBuildReferenceKind.Property, "Foo"),
				(MSBuildReferenceKind.Property, "foo"),
				(MSBuildReferenceKind.Property, "foo")
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
				(MSBuildReferenceKind.Metadata, ("bar", "foo")),
				(MSBuildReferenceKind.Metadata, ("bar", "foo")),
				(MSBuildReferenceKind.Metadata, ("bar", "foo")),
				(MSBuildReferenceKind.Metadata, ("bar", "foo"))
			);
		}

		[Test]
		public void KeywordResolution ()
		{
			var doc = @"
<proj|ect>
  <itemgroup>
    <foo includ|e=""bar"" />
  </itemgroup>
  <target name='Foo' DependsOnT|argets=""@(bar->'%(Foo)')"">
  </target>
</project>".TrimStart ();

			AssertReferences (
				doc,
				(MSBuildReferenceKind.Keyword, "Project"),
				(MSBuildReferenceKind.Keyword, "include"),
				(MSBuildReferenceKind.Keyword, "DependsOnTargets")
			);
		}

		[Test]
		public void TestMarkedIndicesHelper ()
		{
			string str = "|abc|d|efg|hi";
			var indices = MSBuildTestHelpers.GetMarkedIndices (ref str);
			Assert.AreEqual (string.Join (",", indices), "0,3,4,7");
			Assert.AreEqual ("abcdefghi", str);
		}

		[Test]
		public void PropertyFunctionResolution ()
		{
			// HACK
			MSBuildHost.FunctionTypeProvider = new RoslynFunctionTypeProvider ();

			var doc = @"
<project>
  <propertygroup>
    <x condition=""'$(foo.b|ar())'==''"">$(x)$(bar.ba|z())</x>
    <x condition=""'$([foo.b|ar]::ba|z(1,2))'==''"">$([b|ar.a.b]::fo|o())</x>
  </propertygroup>
  <target name='z' DependsOnTargets=""$(bar.fo|o())""/>
  <target name='x' DependsOnTargets=""$([on|e]::|two())""/>
</project>".TrimStart ();

			AssertReferences (
				doc,
				(MSBuildReferenceKind.PropertyFunction, (MSBuildValueKind.Unknown, "bar")),
				(MSBuildReferenceKind.PropertyFunction, (MSBuildValueKind.Unknown, "baz")),
				(MSBuildReferenceKind.ClassName, "foo.bar"),
				(MSBuildReferenceKind.StaticPropertyFunction, ("foo.bar", "baz")),
				(MSBuildReferenceKind.ClassName, "bar.a.b"),
				(MSBuildReferenceKind.StaticPropertyFunction, ("bar.a.b", "foo")),
				(MSBuildReferenceKind.PropertyFunction, (MSBuildValueKind.Unknown, "foo")),
				(MSBuildReferenceKind.ClassName, "one"),
				(MSBuildReferenceKind.StaticPropertyFunction, ("one", "two"))
			);
		}

		[Test]
		public void ItemFunctionResolution ()
		{
			var doc = @"
<project>
  <itemgroup>
    <x include=""@(ay->b|ee())""/>
  </itemgroup>
  <target name='z' Inputs=""@(bar->fo|o())""/>
</project>".TrimStart ();

			AssertReferences (
				doc,
				(MSBuildReferenceKind.ItemFunction, "bee"),
				(MSBuildReferenceKind.ItemFunction, "foo")
			);
		}
	}
}
