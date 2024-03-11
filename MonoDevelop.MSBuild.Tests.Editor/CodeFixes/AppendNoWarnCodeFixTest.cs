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
class AppendNoWarnCodeFixTests : MSBuildEditorTest
{
	[Test]
	public Task TestFixAppendNoWarn ()
	{
		return this.TestCodeFix<AppendNoWarnAnalyzer, AppendNoWarnFixProvider> (
@"<Project>
  <PropertyGroup>
    <NoW|arn>CS1234;CS456</NoWarn>
  </PropertyGroup>
</Project>
",
		"Prepend '$(NoWarn)' to list",
		1,
@"<Project>
  <PropertyGroup>
    <NoWarn>$(NoWarn);CS1234;CS456</NoWarn>
  </PropertyGroup>
</Project>
");
	}
}