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

	[TestCase ("net481", ".NET Framework 4.8.1")]
	[TestCase ("net4.8.1", ".NET Framework 4.8.1")]
	[TestCase ("wpa81", "Windows Phone App 8.1 (Universal Application)")]
	[TestCase ("net6.0-android9000.0", ".NET 6.0 with platform-specific APIs for Android 9000.0")]
	[TestCase ("net6.0-android", ".NET 6.0 with platform-specific APIs for Android 31.0")]
	[TestCase ("net6.0-android32.0", ".NET 6.0 with platform-specific APIs for Android 32.0")]
	[TestCase ("net7.0-android", ".NET 7.0 with platform-specific APIs for Android 33.0")]
	[TestCase ("net6.0", ".NET 6.0")]
	public void FrameworkDescription (string tfm, string description)
	{
		var fx = FrameworkInfoProvider.TryGetFrameworkInfo (tfm);
		Assert.IsNotNull (fx);

		var actualDescription = FrameworkInfoProvider.GetDisplayDescription (fx.Reference);
		Assert.AreEqual (description, actualDescription);
	}

	[TestCase ("net481", ".NETFramework,Version=v4.8.1")]
	[TestCase ("net4.8.1", ".NETFramework,Version=v4.8.1")]
	[TestCase ("wpa81", "WindowsPhoneApp,Version=v8.1")]
	[TestCase ("net6.0-android9000.0", ".NETCoreApp,Version=v6.0 | Android 9000.0")]
	[TestCase ("net6.0-android", ".NETCoreApp,Version=v6.0 | Android 31.0")]
	[TestCase ("net6.0-android32.0", ".NETCoreApp,Version=v6.0 | Android 32.0")]
	[TestCase ("net7.0-android", ".NETCoreApp,Version=v7.0 | Android 33.0")]
	[TestCase ("net6.0", ".NETCoreApp,Version=v6.0")]
	public void FrameworkTitle (string tfm, string description)
	{
		var fx = FrameworkInfoProvider.TryGetFrameworkInfo (tfm);
		Assert.IsNotNull (fx);

		var actualDescription = FrameworkInfoProvider.GetDisplayTitle (fx.Reference);
		Assert.AreEqual (description, actualDescription);
	}
}
