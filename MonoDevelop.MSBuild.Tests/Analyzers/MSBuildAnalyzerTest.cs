// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;

using MonoDevelop.MSBuild.Analysis;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Parser;

using NUnit.Framework;

namespace MonoDevelop.MSBuild.Tests.Analyzers
{
	class MSBuildAnalyzerTest
	{
		protected void VerifyDiagnostics (string source, MSBuildAnalyzer analyzer, params MSBuildDiagnostic[] expectedDiagnostics)
			=> VerifyDiagnostics (source, new[] { analyzer }, expectedDiagnostics);

		protected void VerifyDiagnostics (string source, ICollection<MSBuildAnalyzer> analyzers, params MSBuildDiagnostic[] expectedDiagnostics)
			=> VerifyDiagnostics (source, analyzers, expectedDiagnostics);

		protected void VerifyDiagnostics (string source, ICollection<MSBuildAnalyzer> analyzers, bool includeCoreDiagnostics, params MSBuildDiagnostic[] expectedDiagnostics)
		{
			var token = CancellationToken.None;

			var (env, _) = MSBuildTestEnvironment.EnsureInitialized ();
			var host = env.GetEditorHost ();

			var doc = MSBuildRootDocument.Parse (
				new StringTextSource (source),
				"FakeProject.csproj",
				null,
				host.GetService<MSBuildSchemaProvider> (),
				host.GetService<IRuntimeInformation> (),
				host.GetService<ITaskMetadataBuilder> (),
				token);

			var analyzerDriver = new MSBuildAnalyzerDriver ();

			if (analyzers != null && analyzers.Count > 0) {
				analyzerDriver.AddAnalyzers (analyzers);
			}
			else if (!includeCoreDiagnostics) {
				throw new ArgumentException ("Analyzers can only be null or empty if core diagnostics are included", nameof (analyzers));
			}

			var actualDiagnostics = analyzerDriver.Analyze (doc, includeCoreDiagnostics, token);

			foreach (var expectedDiag in expectedDiagnostics) {
				bool found = false;
				for (int i = 0; i < actualDiagnostics.Count; i++) {
					var actualDiag = actualDiagnostics[i];
					// todo: compare properties
					if (actualDiag.Descriptor == expectedDiag.Descriptor && actualDiag.Span.Equals (expectedDiag.Span)) {
						found = true;
						actualDiagnostics.RemoveAt (i);
						break;
					}
				}
				if (!found) {
					Assert.Fail ($"Did not find expected diagnostic {expectedDiag.Descriptor.Id}@{expectedDiag.Span.Start}-{expectedDiag.Span.End}");
				}
			}

			if (actualDiagnostics.Count > 0) {
				var diag = actualDiagnostics[0];
				Assert.Fail ($"Found unexpected diagnostic {diag.Descriptor.Id}@{diag.Span.Start}-{diag.Span.End}");
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
}
