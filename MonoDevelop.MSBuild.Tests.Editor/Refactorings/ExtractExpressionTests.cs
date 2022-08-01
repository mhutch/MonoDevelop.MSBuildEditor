// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using MonoDevelop.MSBuild.Editor.Refactorings;
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
		public async Task TestExtractExpression ()
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
		public void InsertionPoints ()
		{
			var doc = TextWithMarkers.Parse (
@"<Project>
  <PropertyGroup>^
    ^<Foo>$</Foo>
  </PropertyGroup>
</Project>",
			'^', '$');

			var parser = new XmlTreeParser (new XmlRootState ());
			parser.Parse (doc.Text, preserveWindowsNewlines: true);
			(var parsedDoc, var errors) = parser.FinalizeDocument ();

			Assert.That (errors, Is.Empty);

			var node = parsedDoc.FindAtOffset (doc.GetMarkedPosition ('$'));

			var span = doc.GetMarkedSpan ('^');

			var points = ExtractExpressionRefactoringProvider.GetPropertyInsertionPoints (MSBuildSyntaxKind.Property, node).ToList ();

			Assert.That (points, Has.Count.EqualTo (1));

			Assert.That (points[0].createGroup, Is.False);
			Assert.That (points[0].scopeName, Is.Null);
			Assert.That (points[0].span, Is.EqualTo (span));
		}
	}
}
