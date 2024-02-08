// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.MSBuild.Language.Typesystem;
using MonoDevelop.MSBuild.Schema;

using NUnit.Framework;

namespace MonoDevelop.MSBuild.Tests;

[TestFixture]
internal class DescriptionTests
{
	// soft break, no backticks
	[TestCase (
		"This is a sentence that is very very very very very very very very long",
		"This is a sentence that is very very very very very very..."
		)]
	// soft break, backticks
	[TestCase (
		"This is a `sentence` that is `very` very very very very very very very long",
		"This is a sentence that is very very very very very very..."
		)]
	// hard break, no backticks
	[TestCase (
		"This is a sentence. It is `very` very very very very very very very long",
		"This is a sentence"
		)]
	// hard break, backticks
	[TestCase (
		"This is a `sentence`. It is `very` very very very very very very very long",
		"This is a sentence"
		)]
	// no break, no backticks, no trailing period
	[TestCase (
		"This is a sentence",
		"This is a sentence"
		)]
	// no break, backticks, no trailing period
	[TestCase (
		"This is a `sentence`",
		"This is a sentence"
		)]
	// no break, no backticks, trailing period
	[TestCase (
		"This is a sentence.",
		"This is a sentence"
		)]
	// no break, backticks, trailing period
	[TestCase (
		"This is a `sentence`.",
		"This is a sentence"
		)]
	public void TestCompletionHint (string longDescription, string expectedCompletionHint)
	{
		var symbol = new CustomTypeValue ("foo", longDescription);
		var actual = DescriptionFormatter.GetCompletionHint (symbol);

		Assert.AreEqual (expectedCompletionHint, actual);
	}
}