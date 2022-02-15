// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.MSBuild.Analysis;
using MonoDevelop.MSBuild.Analyzers;

using NUnit.Framework;

namespace MonoDevelop.MSBuild.Tests.Analyzers
{
	[TestFixture]
	class DoNotAssignMSBuildAllProjectsAnalyzerTest : MSBuildAnalyzerTest
	{
		[Test]
		public void SingleAssign ()
		{
			var source = @"<Project>
  <PropertyGroup>
    <SomeOtherProp>Hello</SomeOtherProp>
    <MSBuildAllProjects>$(MSBuildThisProjectFile);$(MSBuildAllProjects)</MSBuildAllProjects>
  </PropertyGroup>
</Project>";

			var analyzer = new DoNotAssignMSBuildAllProjectsAnalyzer ();

			var expected = new MSBuildDiagnostic (
				analyzer.SupportedDiagnostics[0], SpanFromLineColLength (source, 4, 5, 20)
			);

			VerifyDiagnostics (source, analyzer, expected);
		}
	}
}
