// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.MSBuild.Language;

using NUnit.Framework;

namespace MonoDevelop.MSBuild.Tests.Analyzers
{
	[TestFixture]
	class IncompleteDocumentTests : MSBuildAnalyzerTest
	{
		[Test]
		[TestCase ("")]
		[TestCase ("<")]
		[TestCase ("<>")]
		[TestCase ("<Project>\\n<\\n</Project>")]
		public void MinimalDocument (string source)
		{
			VerifyDiagnostics (
				source,
				out var parsedDocument,
				includeCoreDiagnostics: true,
				checkUnexpectedDiagnostics: false
			);

			// reparse to catch any issues in reuse of previous doc
			VerifyDiagnostics (
				source,
				out _,
				includeCoreDiagnostics: true,
				checkUnexpectedDiagnostics: false,
				previousDocument : parsedDocument
			);
		}

		[Test]
		public void MinimalDocumentIncremental ()
		{
			var document = @"<Project>
  <PropertyGroup>
    <Foo>Bar</Foo>
  </PropertyGroup>
</Project>";

			MSBuildRootDocument previousParsedDocument = null;
			for (int i = 1; i < document.Length; i++) {
				var source = document.Substring (0, i);
				if (source[source.Length - 1] == '<') {
					source += ">";
				}
				VerifyDiagnostics (
					source,
					out var parsedDocument,
					includeCoreDiagnostics: true,
					checkUnexpectedDiagnostics: false,
					previousDocument: previousParsedDocument
				);
				previousParsedDocument = parsedDocument;
			}
		}
	}
}
