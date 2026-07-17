// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

using MonoDevelop.MSBuild.Editor;
using MonoDevelop.MSBuild.Editor.Classification;
using MonoDevelop.Xml.Editor.Logging;
using MonoDevelop.Xml.Editor.Parsing;
using MonoDevelop.Xml.Tests;

using NUnit.Framework;

namespace MonoDevelop.MSBuild.Tests.Classification
{
	[TestFixture]
	class MSBuildClassificationTaggerTests : MSBuildEditorTest
	{
		MSBuildClassificationTypeMap CreateTypeMap () => new (Catalog.ClassificationTypeRegistryService);

		/// <summary>
		/// Creates a tagger for a buffer with the given text, waits for the parse, and returns the text and tag of each classification run.
		/// </summary>
		/// <param name="documentText">The document text.</param>
		/// <returns>The classified runs, in the order the tagger returned them.</returns>
		async Task<List<(string Text, ClassificationTag Tag)>> GetClassificationsAsync (string documentText)
		{
			await Catalog.JoinableTaskContext.Factory.SwitchToMainThreadAsync ();

			ITextBuffer buffer = CreateTextBuffer (documentText);
			XmlParserProvider parserProvider = Catalog.GetService<XmlParserProvider> ();
			MSBuildClassificationTagger tagger = new (
				buffer, parserProvider, CreateTypeMap (), Catalog.JoinableTaskContext,
				TestLoggerFactory.CreateTestMethodLogger ().RethrowExceptions ());

			ITextSnapshot snapshot = buffer.CurrentSnapshot;
			await parserProvider.GetParser (buffer).GetOrProcessAsync (snapshot, CancellationToken.None);

			List<(string, ClassificationTag)> results = tagger
				.GetTags (new NormalizedSnapshotSpanCollection (new SnapshotSpan (snapshot, 0, snapshot.Length)))
				.Select (tagSpan => (tagSpan.Span.GetText (), tagSpan.Tag))
				.ToList ();

			tagger.Dispose ();
			return results;
		}

		/// <summary>
		/// Asserts that the runs contain exactly <paramref name="expectedCount"/> runs with the given text and classification type.
		/// </summary>
		/// <param name="runs">The classified runs.</param>
		/// <param name="text">The expected run text.</param>
		/// <param name="expectedTag">The tag whose classification type the runs must have.</param>
		/// <param name="expectedCount">The expected number of matching runs.</param>
		static void AssertRunCount (List<(string Text, ClassificationTag Tag)> runs, string text, ClassificationTag expectedTag, int expectedCount = 1)
			=> Assert.That (
				runs.Count (run => run.Text == text && run.Tag.ClassificationType == expectedTag.ClassificationType),
				Is.EqualTo (expectedCount),
				$"Expected {expectedCount} run(s) of '{text}' classified as '{expectedTag.ClassificationType.Classification}'. Actual runs: {string.Join (", ", runs.Select (r => $"'{r.Text}'={r.Tag.ClassificationType.Classification}"))}");

		[Test]
		public async Task XmlAndExpressionClassifications ()
		{
			MSBuildClassificationTypeMap typeMap = CreateTypeMap ();

			List<(string Text, ClassificationTag Tag)> runs = await GetClassificationsAsync (
@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup Condition=""$(Configuration) == 'Debug'"">
    <Foo>$(Bar);@(Baz);%(Src.Filename)</Foo>
    <!-- comment -->
    <Empty />
  </PropertyGroup>
</Project>");

			// element names, including closing tags; self-closing elements have one name run only
			AssertRunCount (runs, "Project", typeMap.ElementName, 2);
			AssertRunCount (runs, "PropertyGroup", typeMap.ElementName, 2);
			AssertRunCount (runs, "Foo", typeMap.ElementName, 2);
			AssertRunCount (runs, "Empty", typeMap.ElementName, 1);

			// attribute names
			AssertRunCount (runs, "Sdk", typeMap.AttributeName);
			AssertRunCount (runs, "Condition", typeMap.AttributeName);

			// attribute values, including the non-expression segments of expression-containing values
			AssertRunCount (runs, "Microsoft.NET.Sdk", typeMap.AttributeValue);
			AssertRunCount (runs, " == 'Debug'", typeMap.AttributeValue);

			// expression names in attribute values and element text
			AssertRunCount (runs, "Configuration", typeMap.ExpressionName);
			AssertRunCount (runs, "Bar", typeMap.ExpressionName);
			AssertRunCount (runs, "Baz", typeMap.ExpressionName);
			AssertRunCount (runs, "Src", typeMap.ExpressionName);
			AssertRunCount (runs, "Filename", typeMap.ExpressionName);

			// expression delimiters
			AssertRunCount (runs, "$(", typeMap.ExpressionDelimiter, 2);
			AssertRunCount (runs, "@(", typeMap.ExpressionDelimiter, 1);
			AssertRunCount (runs, "%(", typeMap.ExpressionDelimiter, 1);
			AssertRunCount (runs, ")", typeMap.ExpressionDelimiter, 4);

			// comments: delimiters are classified separately, like the VS XML editor does
			AssertRunCount (runs, "<!--", typeMap.Delimiter);
			AssertRunCount (runs, " comment ", typeMap.Comment);
			AssertRunCount (runs, "-->", typeMap.Delimiter);

			// XML punctuation, like the VS XML editor: open tags, close tags, self-closing tags
			AssertRunCount (runs, "<", typeMap.Delimiter, 4);
			AssertRunCount (runs, ">", typeMap.Delimiter, 6);
			AssertRunCount (runs, "</", typeMap.Delimiter, 3);
			AssertRunCount (runs, "/>", typeMap.Delimiter, 1);

			// attribute equals signs and quotes
			AssertRunCount (runs, "=", typeMap.Delimiter, 2);
			AssertRunCount (runs, "\"", typeMap.AttributeQuotes, 4);

			// non-expression segments of element text are classified as XML text
			AssertRunCount (runs, ";", typeMap.Text, 2);
		}

		[Test]
		public async Task PropertyFunctionClassifications ()
		{
			MSBuildClassificationTypeMap typeMap = CreateTypeMap ();

			List<(string Text, ClassificationTag Tag)> runs = await GetClassificationsAsync (
				@"<Project><X Cmd=""$(Foo.Trim())"" N=""$(A.B(true, 5))"" /></Project>");

			AssertRunCount (runs, "Foo", typeMap.ExpressionName);
			AssertRunCount (runs, "Trim", typeMap.FunctionName);
			AssertRunCount (runs, "A", typeMap.ExpressionName);
			AssertRunCount (runs, "B", typeMap.FunctionName);
			AssertRunCount (runs, "true", typeMap.BoolLiteral);
			AssertRunCount (runs, "5", typeMap.NumberLiteral);
		}

		[Test]
		public async Task ProcessingInstructionAndCDataClassifications ()
		{
			MSBuildClassificationTypeMap typeMap = CreateTypeMap ();

			List<(string Text, ClassificationTag Tag)> runs = await GetClassificationsAsync (
				@"<?xml version=""1.0""?><A><![CDATA[xyz]]></A>");

			// processing instruction: delimiters, name, and content are classified separately
			AssertRunCount (runs, "<?", typeMap.Delimiter);
			AssertRunCount (runs, "xml", typeMap.ElementName);
			AssertRunCount (runs, @" version=""1.0""", typeMap.ProcessingInstruction);
			AssertRunCount (runs, "?>", typeMap.Delimiter);

			// CDATA: delimiters and content are classified separately
			AssertRunCount (runs, "<![CDATA[", typeMap.Delimiter);
			AssertRunCount (runs, "xyz", typeMap.CDataSection);
			AssertRunCount (runs, "]]>", typeMap.Delimiter);

			AssertRunCount (runs, "A", typeMap.ElementName, 2);
		}

		[Test]
		public async Task EntityReferenceClassifications ()
		{
			MSBuildClassificationTypeMap typeMap = CreateTypeMap ();

			List<(string Text, ClassificationTag Tag)> runs = await GetClassificationsAsync (
				@"<Project><A Condition=""x &gt; y"">a &amp; b</A></Project>");

			// entity references in attribute values and element text
			AssertRunCount (runs, "&gt;", typeMap.EntityReference);
			AssertRunCount (runs, "&amp;", typeMap.EntityReference);

			// the segments around them keep the attribute value / text classification
			AssertRunCount (runs, "x ", typeMap.AttributeValue);
			AssertRunCount (runs, " y", typeMap.AttributeValue);
			AssertRunCount (runs, "a ", typeMap.Text);
			AssertRunCount (runs, " b", typeMap.Text);
		}

		[Test]
		public async Task UnclosedCommentDoesNotThrow ()
		{
			MSBuildClassificationTypeMap typeMap = CreateTypeMap ();

			List<(string Text, ClassificationTag Tag)> runs = await GetClassificationsAsync ("<Project><!-- oops");

			AssertRunCount (runs, "Project", typeMap.ElementName);
			AssertRunCount (runs, "<!--", typeMap.Delimiter);
			AssertRunCount (runs, " oops", typeMap.Comment);
			AssertRunCount (runs, "-->", typeMap.Delimiter, 0);
		}

		[Test]
		public async Task MalformedDocumentDoesNotThrow ()
		{
			MSBuildClassificationTypeMap typeMap = CreateTypeMap ();

			List<(string Text, ClassificationTag Tag)> runs = await GetClassificationsAsync ("<Project><PropertyGroup><Fo");

			AssertRunCount (runs, "Project", typeMap.ElementName);
			AssertRunCount (runs, "PropertyGroup", typeMap.ElementName);
		}

		[Test]
		public async Task StaleParseResultsAreMappedToRequestedSnapshot ()
		{
			await Catalog.JoinableTaskContext.Factory.SwitchToMainThreadAsync ();

			MSBuildClassificationTypeMap typeMap = CreateTypeMap ();
			ITextBuffer buffer = CreateTextBuffer ("<Project><ItemGroup /></Project>");
			XmlParserProvider parserProvider = Catalog.GetService<XmlParserProvider> ();
			MSBuildClassificationTagger tagger = new (
				buffer, parserProvider, typeMap, Catalog.JoinableTaskContext,
				TestLoggerFactory.CreateTestMethodLogger ().RethrowExceptions ());

			XmlBackgroundParser parser = parserProvider.GetParser (buffer);
			await parser.GetOrProcessAsync (buffer.CurrentSnapshot, CancellationToken.None);

			// LastOutput is assigned in a continuation that may lag behind the parse task itself
			int remainingAttempts = 100;
			while (parser.LastOutput is null && remainingAttempts-- > 0) {
				await Task.Delay (50);
			}
			Assert.That (parser.LastOutput, Is.Not.Null);

			// edit the buffer and request tags immediately, so the tagger likely has to map
			// spans from the last completed parse onto the newer snapshot
			buffer.Insert (buffer.CurrentSnapshot.Length, " ");
			ITextSnapshot editedSnapshot = buffer.CurrentSnapshot;

			List<ITagSpan<ClassificationTag>> tags = tagger
				.GetTags (new NormalizedSnapshotSpanCollection (new SnapshotSpan (editedSnapshot, 0, editedSnapshot.Length)))
				.ToList ();

			Assert.That (tags, Is.Not.Empty);
			foreach (ITagSpan<ClassificationTag> tag in tags) {
				Assert.That (tag.Span.Snapshot, Is.SameAs (editedSnapshot));
			}
			Assert.That (
				tags.Count (tag => tag.Span.GetText () == "Project" && tag.Tag.ClassificationType == typeMap.ElementName.ClassificationType),
				Is.EqualTo (2));

			tagger.Dispose ();
		}

		[Test]
		public void TaggerProviderFallsBackWhenTextMateUnavailable ()
		{
			// with no ICommonEditorAssetServiceFactory available, the provider
			// must fall back to the self-contained classification tagger
			MSBuildTextMateTagger provider = new (
				assetServiceFactory: null,
				Catalog.GetService<XmlParserProvider> (),
				CreateTypeMap (),
				Catalog.JoinableTaskContext,
				Catalog.GetService<IEditorLoggerFactory> ());
			ITextBuffer buffer = CreateTextBuffer ("<Project></Project>");

			ITagger<IClassificationTag>? classificationTagger = provider.CreateTagger<IClassificationTag> (buffer);
			Assert.That (classificationTagger, Is.InstanceOf<MSBuildClassificationTagger> ());

			// the tagger is a per-buffer singleton
			Assert.That (provider.CreateTagger<IClassificationTag> (buffer), Is.SameAs (classificationTagger));

			// no structure tag fallback is needed, as MonoDevelop.Xml's StructureTaggerProvider covers xmlcore content types
			Assert.That (provider.CreateTagger<IStructureTag> (buffer), Is.Null);

			((MSBuildClassificationTagger)classificationTagger!).Dispose ();
		}
	}
}
