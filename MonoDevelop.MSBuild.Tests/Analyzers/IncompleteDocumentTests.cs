// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
	}
}
