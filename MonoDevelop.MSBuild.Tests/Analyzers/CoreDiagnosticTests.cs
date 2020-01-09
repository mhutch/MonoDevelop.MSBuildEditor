// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.MSBuild.Analysis;
using MonoDevelop.MSBuild.Language;

using NUnit.Framework;

namespace MonoDevelop.MSBuild.Tests.Analyzers
{
	[TestFixture]
	class CoreDiagnosticTests : MSBuildAnalyzerTest
	{
		[Test]
		public void NoImports ()
		{
			var source = @"<Project></Project>";

			var expected = new MSBuildDiagnostic (
				CoreDiagnostics.NoTargets, SpanFromLineColLength (source, 1, 2, 7)
			);

			VerifyDiagnostics (source, null, true, expected);
		}
	}
}
