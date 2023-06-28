// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using MonoDevelop.MSBuild.Language.Typesystem;

using NuGet.Frameworks;

namespace MonoDevelop.MSBuild.Schema
{
	// We can't rely on checking the system or the host IDE for frameworks, as they
	// may not be installed. We also can't use NuGet.Frameworks, as it has a rather skewed
	// worldview - it often deals with ranges rather than concrete values, and
	// sometimes omits important things like version numbers
	//
	// FIXME: this should really be something that schemas can extend
	class FrameworkInfoProvider
	{
		public static FrameworkInfoProvider Instance { get; } = new FrameworkInfoProvider ();

		readonly List<KnownFramework> frameworks = new();
		readonly Dictionary<string, KnownFramework> frameworkByShortName = new();

		// shortName is a value that can be used for the TargetFramework property. not all frameworks have this.
		readonly record struct KnownFramework(string? ShortName, string Moniker, Version Version, string? Profile = null, string? Platform = null, Version? PlatformVersion = null);

		static class Platform
		{
			public const string Windows = "Windows";
			public const string Android = "Android";
			public const string iOS = "iOS";
			public const string macOS = "macOS";
			public const string tvOS = "tvOS";
			public const string MacCatalyst = "MacCatalyst";
		}

		static class FxID
		{
			public const string NETFramework = ".NETFramework";
			public const string NETStandard = ".NETStandard";
			public const string NETPortable = ".NETPortable";
			public const string NETCoreApp = ".NETCoreApp";
			public const string MonoAndroid = "MonoAndroid";
			public const string XamarinMac = "Xamarin.Mac";
			public const string XamarinTVOS = "Xamarin.TVOS";
			public const string XamarinWatchOS = "Xamarin.WatchOS";
			public const string XamarinIOS = "Xamarin.iOS";
			public const string MonoUE= "MonoUE";
		}

		public FrameworkInfoProvider ()
		{
			Version CreateVersion (int versionMajor, int versionMinor, int versionBuild) => versionBuild > -1 ? new Version (versionMajor, versionMinor, versionBuild) : new Version (versionMajor, versionMinor);
			void AddLegacy (string? shortName, string moniker, int versionMajor, int versionMinor, int versionBuild = -1, string? profile = null) => frameworks.Add (new KnownFramework (shortName, moniker, CreateVersion (versionMajor, versionMinor, versionBuild), profile));

			void AddNetFx (string shortName, int versionMajor, int versionMinor, int versionBuild = -1, string? profile = null) => AddLegacy (shortName, FxID.NETFramework, versionMajor, versionMinor, versionBuild, profile);

			AddNetFx ("net20", 2, 0);
			AddNetFx ("net30", 3, 0);
			AddNetFx ("net35", 3, 5);
			AddNetFx ("net35-client", 3, 5, profile: "Client");
			AddNetFx ("net40", 4, 0);
			AddNetFx ("net40-client", 4, 0, profile: "Client");
			AddNetFx ("net403", 4, 0);
			AddNetFx ("net45", 4, 5);
			AddNetFx ("net451", 4, 5, 1);
			AddNetFx ("net452", 4, 5, 2);
			AddNetFx ("net46", 4, 6);
			AddNetFx ("net461", 4, 6, 1);
			AddNetFx ("net462", 4, 6, 2);
			AddNetFx ("net47", 4, 7);
			AddNetFx ("net471", 4, 7, 1);
			AddNetFx ("net472", 4, 7, 2);
			AddNetFx ("net48", 4, 8);
			AddNetFx ("net481", 4, 8, 1);


			void AddNetStandard (string shortName, int versionMajor, int versionMinor) => AddLegacy (shortName, FxID.NETStandard, versionMajor, versionMinor);

			AddNetStandard ("netstandard1.0", 1, 0);
			AddNetStandard ("netstandard1.1", 1, 1);
			AddNetStandard ("netstandard1.2", 1, 2);
			AddNetStandard ("netstandard1.3", 1, 3);
			AddNetStandard ("netstandard1.4", 1, 4);
			AddNetStandard ("netstandard1.5", 1, 5);
			AddNetStandard ("netstandard1.6", 1, 6);
			AddNetStandard ("netstandard2.0", 2, 0);
			AddNetStandard ("netstandard2.1", 2, 1);

			// discard the NuGetName as we have not use for it right now
			// but it may become useful in future and this table is a nightmare to construct
			void AddPortable40 (string profile, string nugetName) => AddLegacy (null, FxID.NETPortable, 4, 0, profile: profile);
			void AddPortable45 (string profile, string nugetName) => AddLegacy (null, FxID.NETPortable, 4, 5, profile: profile);
			void AddPortable46 (string profile, string nugetName) => AddLegacy (null, FxID.NETPortable, 4, 6, profile: profile);

			AddPortable46 ("Profile31", "portable-win81+wp81");
			AddPortable46 ("Profile32", "portable-win81+wpa81");
			AddPortable46 ("Profile44", "portable-net451+win81");
			AddPortable46 ("Profile84", "portable-wp81+wpa81");
			AddPortable46 ("Profile151", "portable-net451+win81+wpa81");
			AddPortable46 ("Profile157", "portable-win81+wp81+wpa81");

			AddPortable45 ("Profile7", "portable-net45+win8");
			AddPortable45 ("Profile49", "portable-net45+wp8");
			AddPortable45 ("Profile78", "portable-net45+win8+wp8");
			AddPortable45 ("Profile111", "portable-net45+win8+wpa81");
			AddPortable45 ("Profile259", "portable-net45+win8+wpa81+wp8");

			AddPortable40 ("Profile2", "portable-net4+win8+sl4+wp7");
			AddPortable40 ("Profile3", "portable-net4+sl4");
			AddPortable40 ("Profile4", "portable-net45+sl4+win8+wp7");
			AddPortable40 ("Profile5", "portable-net4+win8");
			AddPortable40 ("Profile6", "portable-net403+win8");
			AddPortable40 ("Profile14", "portable-net4+sl5");
			AddPortable40 ("Profile18", "portable-net403+sl4");
			AddPortable40 ("Profile19", "portable-net403+sl5");
			AddPortable40 ("Profile23", "portable-net45+sl4");
			AddPortable40 ("Profile24", "portable-net45+sl5");
			AddPortable40 ("Profile36", "portable-net4+sl4+win8+wp8");
			AddPortable40 ("Profile37", "portable-net4+sl5+win8");
			AddPortable40 ("Profile41", "portable-net403+sl4+win8");
			AddPortable40 ("Profile42", "portable-net403+sl5+win8");
			AddPortable40 ("Profile46", "portable-net45+sl4+win8");
			AddPortable40 ("Profile47", "portable-net45+sl5+win8");
			AddPortable40 ("Profile88", "portable-net4+sl4+win8+wp75");
			AddPortable40 ("Profile92", "portable-net4+win8+wpa81");
			AddPortable40 ("Profile95", "portable-net403+sl4+win8+wp7");
			AddPortable40 ("Profile96", "portable-net403+sl4+win8+wp75");
			AddPortable40 ("Profile102", "portable-net403+win8+wpa81");
			AddPortable40 ("Profile104", "portable-net45+sl4+win8+wp75");
			AddPortable40 ("Profile136", "portable-net4+sl5+win8+wp8");
			AddPortable40 ("Profile143", "portable-net403+sl4+win8+wp8");
			AddPortable40 ("Profile147", "portable-net403+sl5+win8+wp8");
			AddPortable40 ("Profile154", "portable-net45+sl4+win8+wp8");
			AddPortable40 ("Profile158", "portable-net45+sl5+win8+wp8");
			AddPortable40 ("Profile225", "portable-net4+sl5+win8+wpa81");
			AddPortable40 ("Profile240", "portable-net403+sl5+win8+wpa81");
			AddPortable40 ("Profile255", "portable-net45+sl5+win8+wpa81");
			AddPortable40 ("Profile328", "portable-net4+sl5+win8+wpa81+wp8");
			AddPortable40 ("Profile336", "portable-net403+sl5+win8+wpa81+wp8");
			AddPortable40 ("Profile344", "portable-net45+sl5+win8+wpa81+wp8");


			void AddNetCore (int versionMajor, int versionMinor, string shortname, string? platform = null, Version? platformVersion = null) => frameworks.Add (new KnownFramework (shortname, FxID.NETCoreApp, new Version (versionMajor, versionMinor), null, platform, platformVersion));

			AddNetCore (1, 0, "netcoreapp1.0");
			AddNetCore (1, 1, "netcoreapp1.1");
			AddNetCore (2, 0, "netcoreapp2.0");
			AddNetCore (2, 1, "netcoreapp2.1");
			AddNetCore (2, 2, "netcoreapp2.2");
			AddNetCore (3, 0, "netcoreapp3.0");
			AddNetCore (3, 1, "netcoreapp3.1");

			AddNetCore (5, 0, "net5.0");
			AddNetCore (5, 0, "net5.0-windows");

			AddNetCore (6, 0, "net6.0");
			AddNetCore (6, 0, "net6.0-windows", Platform.Windows);
			AddNetCore (6, 0, "net6.0-android", Platform.Android);
			AddNetCore (6, 0, "net6.0-ios", Platform.iOS);
			AddNetCore (6, 0, "net6.0-maccatalyst", Platform.MacCatalyst);
			AddNetCore (6, 0, "net6.0-macos", Platform.macOS);
			AddNetCore (6, 0, "net6.0-tvos", Platform.tvOS);

			AddNetCore (7, 0, "net7.0");
			AddNetCore (7, 0, "net7.0-windows", Platform.Windows);
			AddNetCore (7, 0, "net7.0-android", Platform.Android);
			AddNetCore (7, 0, "net7.0-ios", Platform.iOS);
			AddNetCore (7, 0, "net7.0-maccatalyst", Platform.MacCatalyst);
			AddNetCore (7, 0, "net7.0-macos", Platform.macOS);
			AddNetCore (7, 0, "net7.0-tvos", Platform.tvOS);

			AddNetCore (8, 0, "net8.0");
			AddNetCore (8, 0, "net8.0-windows", Platform.Windows);
			AddNetCore (8, 0, "net8.0-android", Platform.Android);
			AddNetCore (8, 0, "net8.0-ios", Platform.iOS);
			AddNetCore (8, 0, "net8.0-maccatalyst", Platform.MacCatalyst);
			AddNetCore (8, 0, "net8.0-macos", Platform.macOS);
			AddNetCore (8, 0, "net8.0-tvos", Platform.tvOS);

			AddLegacy (null, FxID.MonoAndroid, 1, 0);
			AddLegacy (null, FxID.MonoAndroid, 2, 3);
			AddLegacy (null, FxID.MonoAndroid, 4, 0, 3);
			AddLegacy (null, FxID.MonoAndroid, 4, 1);
			AddLegacy (null, FxID.MonoAndroid, 4, 2);
			AddLegacy (null, FxID.MonoAndroid, 4, 3);
			AddLegacy (null, FxID.MonoAndroid, 4, 4);
			AddLegacy (null, FxID.MonoAndroid, 4, 4, 87);
			AddLegacy (null, FxID.MonoAndroid, 5, 0);
			AddLegacy (null, FxID.MonoAndroid, 5, 1);
			AddLegacy (null, FxID.MonoAndroid, 6, 0);
			AddLegacy (null, FxID.MonoAndroid, 7, 0);
			AddLegacy (null, FxID.MonoAndroid, 7, 1);
			AddLegacy (null, FxID.MonoAndroid, 8, 0);

			AddLegacy (null, FxID.XamarinMac, 2, 0);
			AddLegacy (null, FxID.XamarinTVOS, 1, 0);
			AddLegacy (null, FxID.XamarinWatchOS, 1, 0);
			AddLegacy (null, FxID.XamarinIOS, 1, 0);
			AddLegacy (null, FxID.MonoUE, 1, 0);

			// sort to make other operations more efficient
			frameworks.Sort ((x, y) => {
				int cmp = string.Compare (x.Moniker, y.Moniker, StringComparison.Ordinal);
				if (cmp != 0) {
					return cmp;
				}
				cmp = x.Version.CompareTo (y.Version);
				if (cmp != 0) {
					return cmp;
				}
				cmp = string.Compare (x.Profile, y.Profile, StringComparison.Ordinal);
				if (cmp != 0) {
					return cmp;
				}
				cmp = string.Compare (x.Platform, y.Platform, StringComparison.Ordinal);
				if (cmp != 0) {
					return cmp;
				}
				cmp = (x.PlatformVersion, y.PlatformVersion) switch {
					(null, null) => 0,
					(Version _, null) => -1,
					(null, Version _) => 1,
					(Version vx, Version vy) => vx.CompareTo (vy),
				};
				return cmp;
			});

			foreach (var fx in frameworks) {
				if (fx.ShortName is not null) {
					frameworkByShortName.Add (fx.ShortName, fx);
				}
			}
		}

		// TODO: better validation. check it's well formed, warn on individual components e.g. unknown version, platform
		public bool IsFrameworkShortNameValid (string shortName) => frameworkByShortName.ContainsKey (shortName);

		/*
		static bool TryParseShortName(string shortName)
		{
			if (shortName.Length < 3) {
				return false;
			}

			if (!shortName.StartsWith ("net", StringComparison.Ordinal)) {
				return false;
			}

			int dashIdx;
			if ((dashIdx = shortName.IndexOf('-')) > 3) {
			}

			return false;
		}

		static bool IsLetterOrDot (char c) => (c >= 65 && c <= 90) || (c >= 97 && c <= 122) || c == 46;
		static bool IsDigitOrDot (char c) => (c >= 48 && c <= 57) || c == 46;
		// letter, digit, dot, dash, or plus
		static bool IsValidProfileChar (char c) => (c >= 48 && c <= 57) || (c >= 65 && c <= 90) || (c >= 97 && c <= 122) || c == 46 || c == 43 || c == 45;
		*/

		public bool IsFrameworkIdentifierValid (string moniker) => GetFrameworksWithMoniker (moniker).Any ();

		public bool IsFrameworkVersionValid (string moniker, Version version) => GetFrameworksWithMonikerAndVersion (moniker, version).Any ();

		IEnumerable<KnownFramework> GetFrameworksWithMoniker (string moniker)
		{
			// take advantage of the sorting to limit the enumeration
			bool foundMoniker = false;
			foreach (var fx in frameworks) {
				if (string.Equals (fx.Moniker, moniker, StringComparison.OrdinalIgnoreCase)) {
					foundMoniker = true;
					yield return fx;
				}
				else if (foundMoniker) {
					yield break;
				}
			}
		}
		IEnumerable<KnownFramework> GetFrameworksWithMonikerAndVersion (string moniker, Version version)
		{
			// take advantage of the sorting to limit the enumeration
			bool foundMoniker = false;
			bool foundVersion = false;
			foreach (var fx in frameworks) {
				if (string.Equals (fx.Moniker, moniker, StringComparison.OrdinalIgnoreCase)) {
					foundMoniker = true;
					if (AreVersionsEquivalent (version, fx.Version)) {
						foundVersion = true;
						yield return fx;
					} else if (foundVersion) {
						yield break;
					}
				} else if (foundMoniker) {
					yield break;
				}
			}
		}

		static NuGetFramework ToNugetFramework (KnownFramework fx) => new (fx.Moniker, fx.Version, fx.Profile ?? "", fx.Platform ?? "", fx.PlatformVersion ?? FrameworkConstants.EmptyVersion);

		public bool IsFrameworkProfileValid (string moniker, Version version, string profile)
		{
			foreach (var fx in GetFrameworksWithMonikerAndVersion (moniker, version)) {
				if (string.Equals (fx.Profile, profile, StringComparison.OrdinalIgnoreCase)) {
					return true;
				}
			}
			return false;
		}

		public IEnumerable<FrameworkInfo> GetFrameworksWithShortNames ()
		{
			foreach (var fx in frameworks) {
				if (fx.ShortName != null) {
					yield return new FrameworkInfo (fx.ShortName, ToNugetFramework (fx));
				}
			}
		}

		public IEnumerable<FrameworkInfo> GetFrameworkIdentifiers ()
		{
			// as they are sorted we can deduplicate by skipping values that are the same as the last returned value
			string? lastId = null;
			foreach (var fx in frameworks) {
				if (string.Equals (fx.Moniker, lastId, StringComparison.Ordinal)) {
					continue;
				}
				lastId = fx.Moniker;
				yield return new FrameworkInfo (fx.Moniker, new NuGetFramework (lastId));
			}
		}

		public IEnumerable<FrameworkInfo> GetFrameworkVersions (string identifier)
		{
			Version? lastReturned = null;
			foreach (var fx in GetFrameworksWithMoniker (identifier)) {
				if (lastReturned is not null && AreVersionsEquivalent (fx.Version, lastReturned)) {
					continue;
				}
				lastReturned = fx.Version;
				yield return new FrameworkInfo ("v" + FormatDisplayVersion (fx.Version), new NuGetFramework (fx.Moniker, fx.Version));
			}
		}

		public IEnumerable<FrameworkInfo> GetFrameworkProfiles (string moniker, Version version)
		{
			foreach (var fx in frameworks) {
				if (!string.Equals (fx.Moniker, moniker, StringComparison.OrdinalIgnoreCase)) {
					continue;
				}
				if (!AreVersionsEquivalent (version, fx.Version)) {
					continue;
				}
				if (fx.Profile is string profile) {
					yield return new FrameworkInfo (profile, new NuGetFramework (fx.Moniker, fx.Version, fx.Profile));
				}
			}
		}

		// some sources of version data use -1 for empty revision and build, some use 0
		public static bool AreVersionsEquivalent (Version v1, Version v2)
		{
			if (v1.Major != v2.Major || v1.Minor != v2.Minor) {
				return false;
			}
			if (v1.Revision > 0 && v1.Revision != v2.Revision) {
				return false;
			}
			if (v1.Build > 0 && v1.Build != v2.Build) {
				return false;
			}
			return true;
		}

		public static string FormatDisplayVersion (Version version)
		{
			if (version.Build > 0) {
				return $"{version.Major}.{version.Minor}.{version.Revision}.{version.Build}";
			}
			if (version.Revision > 0) {
				return $"{version.Major}.{version.Minor}.{version.Revision}";
			}
			return $"{version.Major}.{version.Minor}";
		}

		public static string GetDescription (NuGetFramework fx)
		{
			switch (fx.Framework.ToLowerInvariant ()) {
			case ".netframework":
				return WithVersion (".NET Framework");
			case ".netstandard":
				return WithVersion (".NET Standard");
			case ".netcoreapp":
				if (fx.Version.Major <= 5) {
					return WithVersion (".NET Core");
				}
				return WithVersionAndPlatform (".NET");
			case ".netportable":
				if (string.IsNullOrEmpty (fx.Profile)) {
					return "Portable Class Library";
				}
				string profileNumber = fx.Profile.StartsWith ("Profile", StringComparison.OrdinalIgnoreCase) ? fx.Profile.Substring ("Profile".Length) : fx.Profile;
				return $"Portable Class Library Profile {profileNumber}";
			case "monoandroid":
				return WithVersion ("Xamarin.Android");
			case "xamarinmac":
				return "Xamarin.Mac";
			case "xamarintvos":
				return "Xamarin.tvOS";
			case "xamarinios":
				return "Xamarin.iOS";
			case "xamarinwatchos":
				return "Xamarin.watchOS";
			case "monoue":
				return "Mono for Unreal Engine";
			}
			return null;

			string WithVersion (string description)
			{
				if (fx.Version.Major == 0) {
					return description;
				}
				return $"{description} {FormatDisplayVersion (fx.Version)}";
			}

			string WithVersionAndPlatform (string description)
			{
				if (fx.Version.Major == 0) {
					return description;
				}
				if (string.IsNullOrEmpty (fx.Platform)) {
					return $"{description} {FormatDisplayVersion (fx.Version)}";
				}
				if (fx.PlatformVersion.Major == 0) {
					return $"{description} {FormatDisplayVersion (fx.Version)} framework with platform-specific APIs for {FormatPlatformNameForTitle (fx)}";
				}
				return $"{description} {FormatDisplayVersion (fx.Version)} framework with platform-specific APIs for {FormatPlatformNameForTitle (fx)} {FormatDisplayVersion (fx.PlatformVersion)}";
			}
		}

		public string FormatNameForTitle (NuGetFramework reference)
		{
			var titleName = reference.DotNetFrameworkName;
			if (!reference.HasPlatform) {
				return titleName;
			}

			var sb = new StringBuilder (titleName);
			string platformName = FormatPlatformNameForTitle (reference);

			sb.Append (" | ");
			sb.Append (platformName);
			sb.Append (" ");

			AppendDisplayVersion (sb, reference.PlatformVersion);

			return sb.ToString ();
		}

		static string FormatPlatformNameForTitle (NuGetFramework reference) => reference.Platform.ToLowerInvariant () switch {
			"windows" => Platform.Windows,
			"android" => Platform.Android,
			"ios" => Platform.iOS,
			"macos" => Platform.macOS,
			"tvos" => Platform.tvOS,
			"maccatalyst" => Platform.MacCatalyst,
			_ => reference.Platform
		};

		static void AppendDisplayVersion (StringBuilder sb, Version version)
		{
			if (version.Major <= 0)
				return;
			sb.Append (version.Major);
			sb.Append ('.');
			sb.Append (version.Minor);

			if (version.Build <= 0 && version.Revision <= 0)
				return;
			sb.Append ('.');
			sb.Append (version.Build);

			if (version.Revision <= 0)
				return;
			sb.Append ('.');
			sb.Append (version.Revision);
		}
	}
}
