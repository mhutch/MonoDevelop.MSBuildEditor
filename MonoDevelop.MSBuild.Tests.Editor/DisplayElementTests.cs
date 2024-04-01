// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text.Adornments;
using MonoDevelop.MSBuild.Editor;

using NUnit.Framework;

namespace MonoDevelop.MSBuild.Tests;

[TestFixture]
class DisplayElementTests
{
	[Test]
	public void FormattedDescriptionPlain ()
	{
		var results = DisplayElementFactory.FormatDescriptionText ("foo bar baz").ToList ();

		Assert.AreEqual (1, results.Count);
		AssertTextAndClassification (results[0], "foo bar baz");
	}

	[Test]
	public void FormattedDescriptionMalformed ()
	{
		var results = DisplayElementFactory.FormatDescriptionText ("foo `bar baz").ToList ();

		Assert.AreEqual (1, results.Count);
		AssertTextAndClassification (results[0], "foo `bar baz");
	}

	[Test]
	public void FormattedDescriptionOneElement ()
	{
		var results = DisplayElementFactory.FormatDescriptionText ("foo `bar` baz").ToList ();

		Assert.AreEqual (3, results.Count);
		AssertTextAndClassification (results[0], "foo ");
		AssertTextAndClassification (results[1], "bar", PredefinedClassificationTypeNames.SymbolReference);
		AssertTextAndClassification (results[2], " baz");
	}

	[Test]
	public void FormattedDescriptionTwoElements ()
	{
		var results = DisplayElementFactory.FormatDescriptionText ("foo `bar` baz `z`").ToList ();

		Assert.AreEqual (4, results.Count);
		AssertTextAndClassification (results[0], "foo ");
		AssertTextAndClassification (results[1], "bar", PredefinedClassificationTypeNames.SymbolReference);
		AssertTextAndClassification (results[2], " baz ");
		AssertTextAndClassification (results[3], "z", PredefinedClassificationTypeNames.SymbolReference);
	}

	void AssertTextAndClassification (ClassifiedTextRun run, string text, string classification = PredefinedClassificationTypeNames.NaturalLanguage)
	{
		Assert.AreEqual (text, run.Text);
		Assert.AreEqual (classification, run.ClassificationTypeName);
	}
}
