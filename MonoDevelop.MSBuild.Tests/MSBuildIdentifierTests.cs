// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Syntax;
using MonoDevelop.MSBuild.Language.Typesystem;

using NUnit.Framework;

namespace MonoDevelop.MSBuild.Tests;

[TestFixture]
class MSBuildIdentifierTests
{
	[TestCase ("_FooBar", true)]
	[TestCase ("FooBar", true)]
	[TestCase ("fooBar", true)]
	[TestCase ("foo", true)]
	[TestCase ("Foo", true)]
	[TestCase ("", false)]
	[TestCase ("-FooBar", false)]
	[TestCase (" FooBar", false)]
	[TestCase ("FooBar ", false)]
	[TestCase ("Foo Bar", false)]
	[TestCase ("1Foo", false)]
	[TestCase ("\"Foo", false)]
	public void TestValidIdentifiers (string identifier, bool isValid)
	{
		bool actual = MSBuildIdentifier.IsValid (identifier);
		Assert.AreEqual (isValid, actual);
	}

	[TestCase ("IsFoo", "Is")]
	[TestCase ("_IsFoo", "Is")]
	[TestCase ("_HasFoo", "Has")]
	[TestCase ("Foo", null)]
	[TestCase ("_Foo", null)]
	[TestCase ("__Foo", null)]
	[TestCase (" Foo", null)]
	public void TestGetPrefix (string identifier, string prefix)
	{
		bool success = MSBuildIdentifier.TryGetPrefix (identifier, out var actual);
		Assert.AreEqual (prefix is not null, success);
		Assert.AreEqual (prefix, actual);
	}

	[TestCase ("FooPath", "Path")]
	[TestCase ("_FooEnabled", "Enabled")]
	[TestCase ("_PineappleOn", "On")]
	[TestCase ("Foo", null)]
	[TestCase ("Foo_", null)]
	[TestCase ("_Cat", null)]
	[TestCase ("__Mouse_", null)]
	[TestCase ("Walrus ", null)]
	public void TestGetSuffix (string identifier, string suffix)
	{
		bool success = MSBuildIdentifier.TryGetSuffix (identifier, out var actual);
		Assert.AreEqual (suffix is not null, success);
		Assert.AreEqual (suffix, actual);
	}

	[TestCase ("Enable", MSBuildValueKind.Unknown)]
	[TestCase ("EnableFoo", MSBuildValueKind.Bool)]
	[TestCase ("_EnableFoo", MSBuildValueKind.Bool)]
	[TestCase ("DisableFoo", MSBuildValueKind.Bool)]
	[TestCase ("RequireFoo", MSBuildValueKind.Bool)]
	[TestCase ("UseFoo", MSBuildValueKind.Bool)]
	[TestCase ("AllowFoo", MSBuildValueKind.Bool)]
	[TestCase ("IsFoo", MSBuildValueKind.Bool)]
	[TestCase ("HasFoo", MSBuildValueKind.Bool)]
	[TestCase ("FooEnabled", MSBuildValueKind.Bool)]
	[TestCase ("FooDisabled", MSBuildValueKind.Bool)]
	[TestCase ("FooRequired", MSBuildValueKind.Bool)]
	[TestCase ("RequiredFoo", MSBuildValueKind.Unknown)]
	[TestCase ("FooDependsOn", MSBuildValueKind.TargetName | MSBuildValueKind.ListSemicolon)]
	[TestCase ("FooPath", MSBuildValueKind.FileOrFolder)]
	[TestCase ("FooPaths", MSBuildValueKind.FileOrFolder | MSBuildValueKind.ListSemicolon)]
	[TestCase ("FooDirectory", MSBuildValueKind.Folder)]
	[TestCase ("FooDir", MSBuildValueKind.Folder)]
	[TestCase ("FooFile", MSBuildValueKind.File)]
	[TestCase ("FooFileName", MSBuildValueKind.Filename)]
	[TestCase ("FooFilename", MSBuildValueKind.Filename)]
	[TestCase ("FooUrl", MSBuildValueKind.Url)]
	[TestCase ("FooUri", MSBuildValueKind.Url)]
	[TestCase ("FooExt", MSBuildValueKind.Extension)]
	[TestCase ("FooGuid", MSBuildValueKind.Guid)]
	[TestCase ("FooDirectories", MSBuildValueKind.Folder | MSBuildValueKind.ListSemicolon)]
	[TestCase ("FooDirs", MSBuildValueKind.Folder | MSBuildValueKind.ListSemicolon)]
	[TestCase ("FooFiles", MSBuildValueKind.File | MSBuildValueKind.ListSemicolon)]
	// note: these two are only valid for Property while the rest are valid for Property and Metadata
	[TestCase ("Configuration", MSBuildValueKind.Configuration)]
	[TestCase ("Platform", MSBuildValueKind.Platform)]
	public void TestInferKind (string identifier, MSBuildValueKind kind)
	{
		var actual = MSBuildIdentifier.InferValueKind (identifier, MSBuildSyntaxKind.Property);
		Assert.AreEqual (kind, actual);
	}
}
