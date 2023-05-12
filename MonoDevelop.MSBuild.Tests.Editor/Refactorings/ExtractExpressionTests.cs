// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Text.Editor;

using MonoDevelop.MSBuild.Editor.Refactorings.ExtractExpression;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Language.Syntax;
using MonoDevelop.MSBuild.Util;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Editor.Tests.Extensions;
using MonoDevelop.Xml.Parser;
using MonoDevelop.Xml.Tests.Parser;

using NUnit.Framework;
using NUnit.Framework.Internal;

namespace MonoDevelop.MSBuild.Tests.Editor.Refactorings;

[TestFixture]
class ExtractExpressionTests : MSBuildEditorTest
{
	[Test]
	public Task ExtractFromProperty () => TestExtractExpression (
		textWithMarkers:
@"<Project>
  <PropertyGroup>
    <Foo>|Bar|</Foo>
  </PropertyGroup>
</Project>",
		expectedFixCount: 1,
		invokeFixWithTitle: "Extract expression",
		expectedTextAfterInvoke:
@"<Project>
  <PropertyGroup>
    <MyNewProperty>Bar</MyNewProperty>
    <Foo>$(MyNewProperty)</Foo>
  </PropertyGroup>
</Project>",
		typeText: "NewName",
		expectedTextAfterTyping:
@"<Project>
  <PropertyGroup>
    <NewName>Bar</NewName>
    <Foo>$(NewName)</Foo>
  </PropertyGroup>
</Project>");

	[Test]
	public Task ExtractFromTaskToExistingGroupInProject () => TestExtractExpression (
		textWithMarkers:
@"<Project>
  <Target Name='MyTarget'>
    <Message Text='|Hello there!|' />
  </Target>
</Project>",
		expectedFixCount: 2,
		invokeFixWithTitle: "Extract expression to Project scope",
		expectedTextAfterInvoke:
@"<Project>
  <PropertyGroup>
    <MyNewProperty>Hello there!</MyNewProperty>
  </PropertyGroup>

  <Target Name='MyTarget'>
    <Message Text='$(MyNewProperty)' />
  </Target>
</Project>",
		typeText: "Greeting",
		expectedTextAfterTyping:
@"<Project>
  <PropertyGroup>
    <Greeting>Hello there!</Greeting>
  </PropertyGroup>

  <Target Name='MyTarget'>
    <Message Text='$(Greeting)' />
  </Target>
</Project>");

	async Task TestExtractExpression (string textWithMarkers, int expectedFixCount, string invokeFixWithTitle, string expectedTextAfterInvoke, string typeText, string expectedTextAfterTyping)
	{
		var ctx = await this.GetRefactorings<ExtractExpressionRefactoringProvider> (textWithMarkers);

		Assert.That (ctx.CodeFixes, Has.Count.EqualTo (expectedFixCount));
		Assert.That (ctx.CodeFixes.Select (c => c.Action.Title), Has.One.EqualTo (invokeFixWithTitle));

		var fix = ctx.CodeFixes.Single (c => c.Action.Title == invokeFixWithTitle);

		var operations = await fix.Action.ComputeOperationsAsync (CancellationToken.None);

		var options = ctx.TextView.Options;
		options.SetOptionValue (DefaultOptions.ConvertTabsToSpacesOptionId, true);
		options.SetOptionValue (DefaultOptions.IndentSizeOptionId, 2);
		options.SetOptionValue (DefaultOptions.TabSizeOptionId, 2);

		foreach (var op in operations) {
			op.Apply (options, ctx.TextBuffer, CancellationToken.None, ctx.TextView);
		}

		Assert.That (
			ctx.TextBuffer.CurrentSnapshot.GetText (),
			Is.EqualTo (expectedTextAfterInvoke));

		// type a new name for the extracted property
		await Catalog.JoinableTaskContext.Factory.SwitchToMainThreadAsync (default);
		var commandService = Catalog.CommandServiceFactory.GetService (ctx.TextView);
		commandService.Type (typeText);

		Assert.That (
			ctx.TextBuffer.CurrentSnapshot.GetText (),
			Is.EqualTo (expectedTextAfterTyping));
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

	void CheckExtractionPoints (string textWithMarkers, MSBuildSyntaxKind originKind, params (string scopeName, bool createGroup)[] expectedSpanProps)
	{
		var doc = TextWithMarkers.Parse (textWithMarkers, '^', '$');

		var parser = new XmlTreeParser (new XmlRootState ());
		parser.Parse (doc.Text, preserveWindowsNewlines: true);
		(var parsedDoc, var errors) = parser.FinalizeDocument ();

		Assert.That (errors, Is.Empty);

		var node = parsedDoc.FindAtOffset (doc.GetMarkedPosition ('$'));
		Assert.NotNull (node);

		var expectedSpans = doc.GetMarkedSpans ('^');
		Array.Reverse (expectedSpans);

		var points = ExtractExpressionRefactoringProvider.GetPropertyInsertionPoints (originKind, node).ToList ();

		Assert.That (points, Has.Count.EqualTo (expectedSpans.Length));

		for (int i = 0; i < expectedSpans.Length; i++) {
			Assert.Multiple (() => {
				Assert.That (points[i].span, Is.EqualTo (expectedSpans[i]));
				Assert.That (points[i].scopeName, Is.EqualTo (expectedSpanProps[i].scopeName));
				Assert.That (points[i].createGroup, Is.EqualTo (expectedSpanProps[i].createGroup));
			});
		}
	}

	[TestCase ("This is |an expression|", "an expression")]
	[TestCase ("H|ello $(Perso|n)", "ello $(Person)")]
	[TestCase ("Hello $(Perso||n)", "$(Person)")]
	[TestCase ("Hello %(Perso||n)", null)]
	[TestCase ("|$(Greeting)|, %(Person)", "$(Greeting)")]
	public void ExtractionSelectionExpansion (string textWithMarkers, string? expected)
	{
		var doc = TextWithMarkers.Parse (textWithMarkers, '|');
		var expr = ExpressionParser.Parse (doc.Text, ExpressionOptions.ItemsMetadataAndLists, 0);

		var validatedSpan = ExpressionNodeExtraction.GetValidExtractionSpan (doc.GetMarkedSpan (), expr);

		var validatedExpr = validatedSpan is TextSpan s ? doc.Text.Substring (s.Start, s.Length) : null;

		Assert.That (validatedExpr, Is.EqualTo (expected));
	}
}
