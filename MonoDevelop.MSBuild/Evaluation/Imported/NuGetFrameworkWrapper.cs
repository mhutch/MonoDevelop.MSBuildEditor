// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Simplified version of NuGetFrameworkWrapper that uses our imported NuGetFramework instead of loading the assembly in an AppDomain
//
// partially derived from one or more versions of https://github.com/dotnet/msbuild/blob/main/src/Build/Utilities/NuGetFrameworkWrapper.cs
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using NuGet.Frameworks;

namespace Microsoft.Build.Evaluation;

class NuGetFrameworkWrapper
{
	public static NuGetFrameworkWrapper CreateInstance () => new NuGetFrameworkWrapper ();

	internal string GetTargetFrameworkIdentifier (string tfm)
		=> NuGetFramework.Parse (tfm).Framework;

	internal string GetTargetFrameworkVersion (string tfm, int minVersionPartCount)
		=> GetNonZeroVersionParts (NuGetFramework.Parse (tfm).Version, minVersionPartCount);

	internal bool IsCompatible (string target, string candidate)
		=> DefaultCompatibilityProvider.Instance.IsCompatible (
			NuGetFramework.Parse (target),
			NuGetFramework.Parse (candidate)
		);

	internal string GetTargetPlatformIdentifier (string tfm)
		=> NuGetFramework.Parse (tfm).Platform;

	internal string GetTargetPlatformVersion (string tfm, int minVersionPartCount)
		=> GetNonZeroVersionParts (NuGetFramework.Parse (tfm).PlatformVersion, minVersionPartCount);

	// from https://raw.githubusercontent.com/dotnet/msbuild/7434b575d12157ef98aeaad3b86c8f235f551c41/src/Build/Utilities/NuGetFrameworkWrapper.cs
	private string GetNonZeroVersionParts (Version version, int minVersionPartCount)
	{
		var nonZeroVersionParts = version.Revision == 0 ? version.Build == 0 ? version.Minor == 0 ? 1 : 2 : 3 : 4;
		return version.ToString (Math.Max (nonZeroVersionParts, minVersionPartCount));
	}

	// based on https://raw.githubusercontent.com/dotnet/msbuild/0932b436c6fa26bb356ce21815d5892ed41834d3/src/Build/Utilities/NuGetFrameworkWrapper.cs
	public string FilterTargetFrameworks (string incoming, string filter)
	{
		IEnumerable<(string originalTfm, NuGetFramework parsedTfm)> incomingFrameworks = ParseTfms (incoming);
		IEnumerable<(string originalTfm, NuGetFramework parsedTfm)> filterFrameworks = ParseTfms (filter);
		StringBuilder tfmList = new StringBuilder ();

		// An incoming target framework from 'incoming' is kept if it is compatible with any of the desired target frameworks on 'filter'
		foreach (var l in incomingFrameworks) {
			if (filterFrameworks.Any (
				r => string.Equals (l.parsedTfm.Framework, r.parsedTfm.Framework, StringComparison.OrdinalIgnoreCase) &&
					 ((l.parsedTfm.AllFrameworkVersions && r.parsedTfm.AllFrameworkVersions) || l.parsedTfm.Version == r.parsedTfm.Version)))
			{
				if (tfmList.Length == 0) {
					tfmList.Append (l.originalTfm);
				} else {
					tfmList.Append ($";{l.originalTfm}");
				}
			}
		}

		return tfmList.ToString ();

		IEnumerable<(string originalTfm, NuGetFramework parsedTfm)> ParseTfms (string desiredTargetFrameworks)
		{
			return desiredTargetFrameworks.Split (new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select (tfm => {
				(string originalTfm, NuGetFramework parsedTfm) parsed = (tfm, NuGetFramework.Parse (tfm));
				return parsed;
			});
		}
	}
}