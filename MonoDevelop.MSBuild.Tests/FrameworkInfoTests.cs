// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.MSBuild.Schema;

using NUnit.Framework;

namespace MonoDevelop.MSBuild.Tests;

[TestFixture]
class FrameworkInfoTests
{
	[TestCase ("net48")]
	[TestCase ("net4.8")]
	[TestCase ("sl3")]
	[TestCase ("wp8")]
	[TestCase ("uap10.0.1234")]
	[TestCase ("uap10.0")]
	[TestCase ("net8.0-android")]
	[TestCase ("net8.0")]
	public void ValidFrameworkName (string name)
	{
		var validationResult = FrameworkInfoProvider.Instance.ValidateFrameworkShortName (name, out _, out _, out _, out _, out _);
		Assert.AreEqual (FrameworkNameValidationResult.OK, validationResult);
	}

	[TestCase ("foo")]
	[TestCase ("foo1.0")]
	public void UnknownIdentifier (string name)
	{
		var validationResult = FrameworkInfoProvider.Instance.ValidateFrameworkShortName (name, out _, out _, out _, out _, out _);
		Assert.AreEqual (FrameworkNameValidationResult.UnknownIdentifier, validationResult);
	}

	[TestCase ("net485")]
	[TestCase ("sl6.0")]
	public void UnknownVersion (string name)
	{
		var validationResult = FrameworkInfoProvider.Instance.ValidateFrameworkShortName (name, out _, out _, out _, out _, out _);
		Assert.AreEqual (FrameworkNameValidationResult.UnknownVersion, validationResult);
	}

	[TestCase ("net481-client")]
	[TestCase ("net35-bad")]
	public void UnknownProfile (string name)
	{
		var validationResult = FrameworkInfoProvider.Instance.ValidateFrameworkShortName (name, out _, out _, out _, out _, out _);
		Assert.AreEqual (FrameworkNameValidationResult.UnknownProfile, validationResult);
	}

	[TestCase ("net6.0-fridge")]
	public void UnknownPlatform (string name)
	{
		var validationResult = FrameworkInfoProvider.Instance.ValidateFrameworkShortName (name, out _, out _, out _, out _, out _);
		Assert.AreEqual (FrameworkNameValidationResult.UnknownPlatform, validationResult);
	}
}
