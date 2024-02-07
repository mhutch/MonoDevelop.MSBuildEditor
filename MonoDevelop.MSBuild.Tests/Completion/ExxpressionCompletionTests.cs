// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Linq;

using MonoDevelop.MSBuild.Analysis;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Typesystem;
using MonoDevelop.MSBuild.Schema;

using NUnit.Framework;

namespace MonoDevelop.MSBuild.Tests.Completion;

[TestFixture]
class ExpressionCompletionTests : MSBuildExpressionCompletionTest
{
	[Test]
	public void WarningCodes ()
	{
		var source = @"<Project>
  <PropertyGroup>
    <NoWarn>|</NoWarn>
  </PropertyGroup>
</Project>";


		var schema = new MSBuildSchema () {
			new CustomTypeInfo (
				[
					new CustomTypeValue ("CS123", null),
					new CustomTypeValue ("CS456", null)
				],
				"csharp-warnings",
				baseKind: MSBuildValueKind.WarningCode
			),
			new PropertyInfo ("NoWarn", "", MSBuildValueKind.WarningCode)
		};

		var completions = GetExpressionCompletion (source, out _, schema: schema).ToArray ();

		Assert.AreEqual (2, completions.Length);
		Assert.AreEqual ("CS123", completions[0].Name);
		Assert.AreEqual ("CS456", completions[1].Name);
	}
}
