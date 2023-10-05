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
			VerifyDiagnostics (source, null, includeCoreDiagnostics: true, checkUnexpectedDiagnostics: false);
		}
	}
}
