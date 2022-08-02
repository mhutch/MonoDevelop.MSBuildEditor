// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using MonoDevelop.MSBuild.Editor.Refactorings.ExtractExpression;
using MonoDevelop.MSBuild.Language.Syntax;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Editor.Tests.Extensions;
using MonoDevelop.Xml.Parser;
using MonoDevelop.Xml.Tests.Parser;

using NUnit.Framework;
using NUnit.Framework.Internal;

namespace MonoDevelop.MSBuild.Tests.Editor.Refactorings
{
	[TestFixture]
	class ExtractExpressionTests : MSBuildEditorTest
	{
		[Test]
		public async Task ExtractFromProperty ()
		{
			var ctx = await this.GetRefactorings<ExtractExpressionRefactoringProvider> (
@"<Project>
  <PropertyGroup>
    <Foo>$Bar$</Foo>
  </PropertyGroup>
</Project>");

			Assert.That (ctx.CodeFixes, Has.Count.EqualTo (1));

			var operations = await ctx.CodeFixes[0].Action.ComputeOperationsAsync (CancellationToken.None);

			foreach (var op in operations) {
				op.Apply (ctx.TextView?.Options, ctx.TextBuffer, CancellationToken.None, ctx.TextView);
			}

			Assert.That (
				ctx.TextBuffer.CurrentSnapshot.GetText (),
				Is.EqualTo (
@"<Project>
  <PropertyGroup>
    <MyNewProperty>Bar</MyNewProperty>
    <Foo>$(MyNewProperty)</Foo>
  </PropertyGroup>
</Project>"
			));

			await Catalog.JoinableTaskContext.Factory.SwitchToMainThreadAsync (default);
			var commandService = Catalog.CommandServiceFactory.GetService (ctx.TextView);
			commandService.Type ("NewName");

			Assert.That (
				ctx.TextBuffer.CurrentSnapshot.GetText (),
				Is.EqualTo (
@"<Project>
  <PropertyGroup>
    <NewName>Bar</NewName>
    <Foo>$(NewName)</Foo>
  </PropertyGroup>
</Project>"
			));
		}

		[Test]
		public void ExtractionPointsFromProperty ()
		{
			CheckExtractionPoints (
@"<Project>
  <PropertyGroup>^
    ^<Foo>$</Foo>
  </PropertyGroup>
</Project>",
				MSBuildSyntaxKind.Property,
				("PropertyGroup", false));
		}

		[Test]
		public void ExtractionPointsFromItem ()
		{
			CheckExtractionPoints (
@"<Project>^
  ^<ItemGroup>
    <Foo Include='$' />
  </ItemGroup>
</Project>",
				MSBuildSyntaxKind.Item,
				("Project", true));
		}
		[Test]
		public void ExtractionPointsFromItemToExistingPropertyGroup ()
		{
			CheckExtractionPoints (
@"<Project>
  <PropertyGroup>
    <Hello>World</Hello>^
  ^</PropertyGroup>
  <ItemGroup>
    <Foo Include='$' />
  </ItemGroup>
</Project>",
				MSBuildSyntaxKind.Item,
				("Project", false));
		}

		[Test]
		public void ExtractionPointsFromTarget ()
		{
			CheckExtractionPoints (
@"<Project>^
  ^<Target Name='MyTarget'>^
    ^<SomeTask Arg='$' />
  </Target>
</Project>",
				MSBuildSyntaxKind.Task,
				("Target", true), ("Project", true));
		}

		void CheckExtractionPoints (string textWithMarker, MSBuildSyntaxKind originKind, params (string scopeName, bool createGroup)[] expectedSpanProps)
		{
			var doc = TextWithMarkers.Parse (textWithMarker, '^', '$');

			var parser = new XmlTreeParser (new XmlRootState ());
			parser.Parse (doc.Text, preserveWindowsNewlines: true);
			(var parsedDoc, var errors) = parser.FinalizeDocument ();

			Assert.That (errors, Is.Empty);

			var node = parsedDoc.FindAtOffset (doc.GetMarkedPosition ('$'));

			var expectedSpans = doc.GetMarkedSpans ('^');
			Array.Reverse (expectedSpans);

			var points = ExtractExpressionRefactoringProvider.GetPropertyInsertionPoints (originKind, node).ToList ();

			Assert.That (points, Has.Count.EqualTo (expectedSpans.Length));

			for (int i = 0; i < expectedSpans.Length; i++) {
				Assert.That (points[i].span, Is.EqualTo (expectedSpans[i]));
				Assert.That (points[i].scopeName, Is.EqualTo (expectedSpanProps[i].Item1));
				Assert.That (points[i].createGroup, Is.EqualTo (expectedSpanProps[i].Item2));
			}
		}
	}
}
