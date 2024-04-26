// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace MonoDevelop.MSBuild.Schema
{
	partial class FrameworkInfoProvider
	{
		static class KnownPlatform
		{
			public const string Windows = "Windows";
			public const string Android = "Android";
			public const string iOS = "iOS";
			public const string macOS = "macOS";
			public const string tvOS = "tvOS";
			public const string MacCatalyst = "MacCatalyst";
			public const string Tizen = "Tizen";
			public const string Browser = "Browser";

			static class LowerId
			{
				public const string Windows = "windows";
				public const string Android = "android";
				public const string iOS = "ios";
				public const string macOS = "macos";
				public const string tvOS = "tvos";
				public const string MacCatalyst = "maccatalyst";
				public const string Tizen = "tizen";
				public const string Browser = "browser";
			}

			public static string ToLowerCase (string platform) => platform switch {
				Windows => LowerId.Windows,
				Android => LowerId.Android,
				iOS => LowerId.iOS,
				macOS => LowerId.macOS,
				tvOS => LowerId.tvOS,
				MacCatalyst => LowerId.MacCatalyst,
				Tizen => LowerId.Tizen,
				Browser => LowerId.Browser,
				_ => platform.ToLowerInvariant ()
			};

			public static string ToCanonicalCase (string platform)
			{
				return CanonicalCaseFromLower (platform)
					?? CanonicalCaseFromLower (platform.ToLowerInvariant ())
					?? platform;

				static string? CanonicalCaseFromLower (string platform) => platform switch {
					LowerId.Windows => Windows,
					LowerId.Android => Android,
					LowerId.iOS => iOS,
					LowerId.macOS => macOS,
					LowerId.tvOS => tvOS,
					LowerId.MacCatalyst => MacCatalyst,
					LowerId.Tizen => Tizen,
					LowerId.Browser => Browser,
					_ => null
				};
			}

			public static bool TryGetDefaultPlatformVersion (int netcoreappMajorVersion, string platform, [NotNullWhen (true)] out Version? defaultPlatformVersion)
			{
				return defaultPlatformVersions.TryGetValue ((netcoreappMajorVersion, ToLowerCase (platform)), out defaultPlatformVersion);
			}

			static readonly Dictionary<(int, string), Version> defaultPlatformVersions = new () {
				{ (6, LowerId.Android), new Version (31, 0) },
				{ (7, LowerId.Android), new Version (33, 0) },
				{ (8, LowerId.Android), new Version (34, 0) },
				{ (6, LowerId.iOS), new Version (15, 0) },
				{ (7, LowerId.iOS), new Version (16, 1) },
				{ (8, LowerId.iOS), new Version (17, 2) },
				{ (5, LowerId.MacCatalyst), new Version (15, 0) },
				{ (7, LowerId.MacCatalyst), new Version (16, 1) },
				{ (8, LowerId.MacCatalyst), new Version (17, 2) },
				{ (6, LowerId.macOS), new Version (12, 0) },
				{ (7, LowerId.macOS), new Version (13, 0) },
				{ (8, LowerId.macOS), new Version (14, 2) },
				{ (6, LowerId.tvOS), new Version (15, 1) },
				{ (7, LowerId.tvOS), new Version (16, 1) },
				{ (8, LowerId.tvOS), new Version (17, 1) },
				{ (7, LowerId.Tizen), new Version (7, 0) },
				{ (8, LowerId.Tizen), new Version (8, 0) },
				{ (6, LowerId.Windows), new Version (7, 0) },
				{ (7, LowerId.Windows), new Version (7, 0) },
				{ (8, LowerId.Windows), new Version (7, 0) },
			};
		}
	}
}
