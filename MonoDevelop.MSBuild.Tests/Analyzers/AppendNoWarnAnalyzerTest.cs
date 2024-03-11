// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.MSBuild.Analysis;
using MonoDevelop.MSBuild.Analyzers;

using NUnit.Framework;

namespace MonoDevelop.MSBuild.Tests.Analyzers
{
	[TestFixture]
	class AppendNoWarnAnalyzerTest : MSBuildDocumentTest
	{
		[TestCase("CS123;CS346;CS567", true)]
		[TestCase("CS123", true)]
		[TestCase("$(NoWarn)CS234", true)]
		[TestCase("$(NoWarn);CS123", false)]
		[TestCase("CS563;   $(NoWarn)  ;CS123", false)]
		[TestCase("  $(NoWarn)  ", false)]
		[TestCase("$(NoWarn);", false)]
		public void ValueInSimpleProject (string value, bool isError)
		{
			var source = $@"<Project>
  <PropertyGroup>
    <NoWarn>{value}</NoWarn>
  </PropertyGroup>
</Project>";

			var analyzer = new AppendNoWarnAnalyzer ();

			MSBuildDiagnostic[] expected = isError
				? [new MSBuildDiagnostic (analyzer.SupportedDiagnostics[0], MSBuildDocumentTest.SpanFromLineColLength (source, 3, 6, 6))]
				: [];

			MSBuildDocumentTest.VerifyDiagnostics (source, [ analyzer ], checkUnexpectedDiagnostics: true, expectedDiagnostics: expected);
		}
	}
}
