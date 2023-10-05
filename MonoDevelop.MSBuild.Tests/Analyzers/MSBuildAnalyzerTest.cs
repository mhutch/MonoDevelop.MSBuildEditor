// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

using MonoDevelop.MSBuild.Analysis;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Parser;
using MonoDevelop.Xml.Tests;

using NUnit.Framework;

namespace MonoDevelop.MSBuild.Tests.Analyzers
{
	class MSBuildAnalyzerTest
	{
		[OneTimeSetUp]
		public void LoadMSBuild () => MSBuildTestHelpers.RegisterMSBuildAssemblies ();

		protected void VerifyDiagnostics (string source, MSBuildAnalyzer analyzer, params MSBuildDiagnostic[] expectedDiagnostics)
			=> VerifyDiagnostics (source, out _, new[] { analyzer }, false, false, null, expectedDiagnostics);

		protected void VerifyDiagnostics (string source, ICollection<MSBuildAnalyzer> analyzers, params MSBuildDiagnostic[] expectedDiagnostics)
			=> VerifyDiagnostics (source, out _, analyzers, false, false, null, expectedDiagnostics);

		protected void VerifyDiagnostics (string source, ICollection<MSBuildAnalyzer> analyzers, bool includeCoreDiagnostics, params MSBuildDiagnostic[] expectedDiagnostics)
			=> VerifyDiagnostics (source, out _, analyzers, includeCoreDiagnostics, false, null, expectedDiagnostics);

		protected void VerifyDiagnostics (string source, ICollection<MSBuildAnalyzer> analyzers, bool includeCoreDiagnostics, bool checkUnexpectedDiagnostics, params MSBuildDiagnostic[] expectedDiagnostics)
			=> VerifyDiagnostics (source, out _, analyzers, includeCoreDiagnostics, checkUnexpectedDiagnostics, null, expectedDiagnostics);

		protected void VerifyDiagnostics (
			string source,
			out MSBuildRootDocument parsedDocument,
			ICollection<MSBuildAnalyzer> analyzers = null,
			bool includeCoreDiagnostics = false,
			bool checkUnexpectedDiagnostics = false,
			MSBuildSchema schema = null,
			MSBuildDiagnostic[] expectedDiagnostics = null,
			MSBuildRootDocument previousDocument = null
			)
		{
			const string projectFileName = "FakeProject.csproj";

			var token = CancellationToken.None;

			expectedDiagnostics ??= Array.Empty<MSBuildDiagnostic> ();

			var schemas = new TestSchemaProvider ();
			if (schema is not null) {
				schemas.AddTestSchema (projectFileName, null, schema);
			}

			var environment = new NullMSBuildEnvironment ();
			var taskMetadataBuilder = new NoopTaskMetadataBuilder ();

			// internal errors should cause test failure
			var logger = TestLoggerFactory.CreateTestMethodLogger ();
			logger = new ExceptionRethrowingLogger (logger);

			parsedDocument = MSBuildRootDocument.Parse (
				new StringTextSource (source),
				projectFileName,
				previousDocument,
				schemas,
				environment,
				taskMetadataBuilder,
				logger,
				token);

			var analyzerDriver = new MSBuildAnalyzerDriver (logger);

			if (analyzers != null && analyzers.Count > 0) {
				analyzerDriver.AddAnalyzers (analyzers);
			} else if (!includeCoreDiagnostics) {
				throw new ArgumentException ("Analyzers can only be null or empty if core diagnostics are included", nameof (analyzers));
			}

			var actualDiagnostics = analyzerDriver.Analyze (parsedDocument, includeCoreDiagnostics, token);

			foreach (var expectedDiag in expectedDiagnostics) {
				bool found = false;
				for (int i = 0; i < actualDiagnostics.Count; i++) {
					var actualDiag = actualDiagnostics[i];
					if (actualDiag.Descriptor == expectedDiag.Descriptor && actualDiag.Span.Equals (expectedDiag.Span)) {
						Assert.That (actualDiag.Properties ?? Enumerable.Empty<KeyValuePair<string,object>>(),
							Is.EquivalentTo (expectedDiag.Properties ?? Enumerable.Empty<KeyValuePair<string, object>> ())
							.UsingDictionaryComparer<string,object> ());
						found = true;
						actualDiagnostics.RemoveAt (i);
						break;
					}
				}
				if (!found) {
					Assert.Fail ($"Did not find expected diagnostic {expectedDiag.Descriptor.Id}@{expectedDiag.Span.Start}-{expectedDiag.Span.End}");
				}
			}

			if (checkUnexpectedDiagnostics && actualDiagnostics.Count > 0) {
				Assert.Fail ($"Found unexpected diagnostics: {string.Join ("", actualDiagnostics.Select (diag => $"\n\t{diag.Descriptor.Id}@{diag.Span.Start}-{diag.Span.End}"))}");
			}
		}

		protected TextSpan SpanFromLineColLength (string text, int line, int startCol, int length)
		{
			int currentLine = 1, currentCol = 1;
			for (int offset = 0; offset < text.Length; offset++) {
				if (currentLine == line && currentCol == startCol) {
					return new TextSpan (offset, length);
				}
				char c = text[offset];
				switch (c) {
				case '\r':
					if (offset + 1 < text.Length && text[offset + 1] == '\n') {
						offset++;
					}
					goto case '\n';
				case '\n':
					if (currentLine == line) {
						throw new ArgumentOutOfRangeException ($"Line {currentLine} ended at col {currentCol}");
					}
					currentLine++;
					currentCol = 1;
					break;
				default:
					currentCol++;
					break;
				}
			}
			throw new ArgumentOutOfRangeException ($"Reached line {currentLine}");
		}

	}

	class TestSchemaProvider : MSBuildSchemaProvider
	{
		readonly Dictionary<(string filename, string sdk), MSBuildSchema> schemas = new ();

		public void AddTestSchema (string filename, string sdk, MSBuildSchema schema)
		{
			schemas.Add ((filename, sdk), schema);
		}

		public override MSBuildSchema GetSchema (string path, string sdk, out IList<MSBuildSchemaLoadError> loadErrors)
		{
			if (schemas.TryGetValue ((Path.GetFileName (path), sdk), out MSBuildSchema schema)) {
				loadErrors = null;
				return schema;
			}

			return base.GetSchema (path, sdk, out loadErrors);
		}
	}
}
