// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

using MonoDevelop.MSBuild.Editor.Roslyn;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Typesystem;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.MSBuild.Util;
using MonoDevelop.Xml.Parser;
using MonoDevelop.Xml.Tests;

using NUnit.Framework;

namespace MonoDevelop.MSBuild.Tests
{
	[TestFixture]
	class MSBuildFindReferencesTests : MSBuildDocumentTest
	{
		List<(int Offset, int Length, ReferenceUsage Usage)> FindReferences (string docString, MSBuildReferenceKind kind, object reference, MSBuildSchema schema = null, [CallerMemberName] string testMethodName = null)
		{
			var textDoc = new StringTextSource (docString);

			var xmlParser = new XmlTreeParser (new XmlRootState ());
			var (xdoc, _) = xmlParser.Parse (new StringReader (docString));

			var logger = TestLoggerFactory.CreateLogger (testMethodName).RethrowExceptions ();

			var doc = new MSBuildDocument ("SomeImportedProps.props", false) {
				Schema = schema,
			};
			var parseContext = new MSBuildParserContext (
				new NullMSBuildEnvironment (), null, null, null, "SomeRootProject.csproj", new PropertyValueCollector (false), null, logger, null, default);
			doc.Build (xdoc, parseContext);

			var functionTypeProvider = new RoslynFunctionTypeProvider (null, parseContext.Logger);

			var results = new List<(int Offset, int Length, ReferenceUsage Usage)> ();
			var collector = MSBuildReferenceCollector.Create (
				doc, textDoc,logger,
				new MSBuildResolver.MSBuildMutableResolveResult {
					ReferenceKind = kind,
					Reference = reference,
				}.AsImmutable (),
				functionTypeProvider, results.Add);
			collector.Run (xdoc.RootElement);
			return results;
		}

		void AssertLocations (
			string doc, string expectedValue,
			List<(int Offset, int Length, ReferenceUsage Usage)> actual,
			params (int Offset, int Length, ReferenceUsage Usage)[] expected
			)
			=> AssertLocations (doc, actual, expected.Select (e => (expectedValue, e.Offset, e.Length, e.Usage)).ToArray ());

		void AssertLocations (
			string doc,
			List<(int Offset, int Length, ReferenceUsage Usage)> actual,
			params (string expectedValue, int Offset, int Length, ReferenceUsage Usage)[] expected)
		{
			if (actual.Count != expected.Length) {
				DumpLocations ();
				Assert.Fail ($"List has {actual.Count} items, expected {expected.Length}");
			}

			for (int i = 0; i < actual.Count; i++) {
				var (offset, length, usage) = actual[i];
				var (expectedName, expectedOffset, expectedLength, expectedUsage) = expected[i];
				if (offset != expectedOffset || length != expectedLength || usage != expectedUsage || !string.Equals (expectedName, doc.Substring (offset, length), StringComparison.OrdinalIgnoreCase)) {
					DumpLocations ();
					Assert.Fail ($"Position {i}: expected ({expectedOffset}, {expectedLength})='{expectedName}' ({expectedUsage}), got ({offset}, {length})='{doc.Substring (offset, length)}' ({usage})");
				}
			}

			void DumpLocations ()
			{
				Console.WriteLine ("Locations: ");
				foreach (var (offset, length, usage) in actual) {
					Console.WriteLine ($"    ({offset}, {length})='{doc.Substring (offset, length)}'");
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
</project>".TrimStart ().Replace ("\r", "");

			var refs = FindReferences (doc, MSBuildReferenceKind.Item, "Foo");

			AssertLocations (
				doc, "foo", refs,
				(29, 3, ReferenceUsage.Write),
				(56, 3, ReferenceUsage.Read),
				(76, 3, ReferenceUsage.Read),
				(109, 3, ReferenceUsage.Read),
				(199, 3, ReferenceUsage.Read),
				(236, 3, ReferenceUsage.Read),
				(244, 3, ReferenceUsage.Read)
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
</project>".TrimStart ().Replace ("\r", "");

			var refs = FindReferences (doc, MSBuildReferenceKind.Property, "Foo");

			AssertLocations (
				doc, "foo", refs,
				(33, 3, ReferenceUsage.Write),
				(52, 3, ReferenceUsage.Read),
				(69, 3, ReferenceUsage.Read),
				(140, 3, ReferenceUsage.Read),
				(199, 3, ReferenceUsage.Read),
				(229, 3, ReferenceUsage.Read),
				(252, 3, ReferenceUsage.Read),
				(287, 3, ReferenceUsage.Read),
				(344, 3, ReferenceUsage.Read)
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
</project>".TrimStart ().Replace ("\r", "");

			var refs = FindReferences (doc, MSBuildReferenceKind.Metadata, ("bar", "foo"));

			AssertLocations (
				doc, "foo", refs,
				(33, 3, ReferenceUsage.Write),
				(68, 3, ReferenceUsage.Write),
				(165, 3, ReferenceUsage.Read),
				(216, 3, ReferenceUsage.Read),
				(280, 3, ReferenceUsage.Write),
				(367, 3, ReferenceUsage.Read)
			);
		}

		[Test]
		public void FindKnownValueReferences()
		{
			var doc = @"<Project>
  <PropertyGroup>
    <Greetings>  |Hello| ; Bye;|Hi|  ; See Ya</Greetings>
    <Greetings>|Hi|</Greetings>
    <Goodbyes>Hi</Goodbyes>
    <OtherGreetings>|Hi|</OtherGreetings>
  </PropertyGroup>
  <ItemGroup>
    <GreetingItem Include=""|Hi|"" Foo=""Hi"" Blah=""|Hello|"" />
  </ItemGroup>
</Project>";

			var textWithMarkers = TextWithMarkers.Parse (doc, '|');


			var schema = new MSBuildSchema ();

			var customType = new CustomTypeInfo ([
				new CustomTypeValue("Hello", null, aliases: [ "Hi" ]),
				new CustomTypeValue("Welcome", null)
			]);
			var greetingsProp = new PropertyInfo ("Greetings", "", valueKind: MSBuildValueKind.CustomType.AsList (), customType: customType);
			schema.Properties.Add (greetingsProp.Name, greetingsProp);
			var otherProp = new PropertyInfo ("OtherGreetings", "", valueKind: MSBuildValueKind.CustomType, customType: customType);
			schema.Properties.Add (otherProp.Name, otherProp);
			var greetingItem= new ItemInfo ("GreetingItem", "", valueKind: MSBuildValueKind.CustomType, customType: customType, metadata: new Dictionary<string, MetadataInfo> {
				{ "Blah", new MetadataInfo("Blah", null, valueKind: MSBuildValueKind.CustomType, customType: customType) },
				{ "Foo", new MetadataInfo("Foo", null, valueKind: MSBuildValueKind.String) }
			});
			schema.Items.Add (greetingItem.Name, greetingItem);

			var refs = FindReferences (textWithMarkers.Text, MSBuildReferenceKind.KnownValue, customType.Values[0], schema);

			AssertLocations (
				textWithMarkers.Text, refs,
				textWithMarkers.GetMarkedSpans('|')
					.Select (span => (textWithMarkers.Text.Substring(span.Start, span.Length), span.Start, span.Length, ReferenceUsage.Read))
					.ToArray ()
			);
		}
	}
}
