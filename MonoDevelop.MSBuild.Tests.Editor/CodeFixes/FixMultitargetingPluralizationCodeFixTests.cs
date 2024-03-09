// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System.Threading.Tasks;

using MonoDevelop.MSBuild.Analyzers;
using MonoDevelop.MSBuild.Editor.CodeFixes;

using NUnit.Framework;
using NUnit.Framework.Internal;

namespace MonoDevelop.MSBuild.Tests.Editor.CodeFixes;

[TestFixture]
class FixMultitargetingPluralizationCodeFixTests : MSBuildEditorTest
{
	[Test]
	public Task DepluralizeTargetFrameworks ()
	{
		return this.TestCodeFix<TargetFrameworksOrTargetFrameworkAnalyzer, FixMultitargetingPluralizationFixProvider> (
@"<Project>
  <PropertyGroup>
    <TargetFra|meworks>net8.0</TargetFrameworks>
  </PropertyGroup>
</Project>
",
			"Change 'TargetFrameworks' to 'TargetFramework'",
			1,
@"<Project>
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>
");
	}

	[Test]
	public Task PluralizeTargetFramework ()
	{
			return this.TestCodeFix<TargetFrameworksOrTargetFrameworkAnalyzer, FixMultitargetingPluralizationFixProvider> (
@"<Project>
  <PropertyGroup>
    <Tar|getFramework>net48;net8.0</TargetFramework>
  </PropertyGroup>
</Project>
",
			"Change 'TargetFramework' to 'TargetFrameworks'",
			1,
@"<Project>
  <PropertyGroup>
    <TargetFrameworks>net48;net8.0</TargetFrameworks>
  </PropertyGroup>
</Project>
");
	}

	[Test]
	public Task DepluralizeRuntimeIdentifiers ()
	{
		return this.TestCodeFix<RuntimeIdentifierOrRuntimeIdentifiersAnalyzer, FixMultitargetingPluralizationFixProvider> (
@"<Project>
  <PropertyGroup>
    <Runtim|eIdentifiers>windows-x64</RuntimeIdentifiers>
  </PropertyGroup>
</Project>
",
			"Change 'RuntimeIdentifiers' to 'RuntimeIdentifier'",
			1,
@"<Project>
  <PropertyGroup>
    <RuntimeIdentifier>windows-x64</RuntimeIdentifier>
  </PropertyGroup>
</Project>
");
	}
	[Test]
	public Task PluralizeRuntimeIdentifier ()
	{
		return this.TestCodeFix<RuntimeIdentifierOrRuntimeIdentifiersAnalyzer, FixMultitargetingPluralizationFixProvider> (
@"<Project>
  <PropertyGroup>
    <Runt|imeIdentifier>windows-x64;linux-x64</RuntimeIdentifier>
  </PropertyGroup>
</Project>
",
			"Change 'RuntimeIdentifier' to 'RuntimeIdentifiers'",
			1,
@"<Project>
  <PropertyGroup>
    <RuntimeIdentifiers>windows-x64;linux-x64</RuntimeIdentifiers>
  </PropertyGroup>
</Project>
");
	}
}