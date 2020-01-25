// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Frameworks;

namespace MonoDevelop.MSBuild.Schema
{
	// We can't rely on checking the system or the host IDE for frameworks, as they
	// may not be installed. We also can't use NuGet.Frameworks, as it has a rather skewed
	// worldview and sometimes omits important things like version numbers
	class FrameworkInfoProvider
	{
		public static FrameworkInfoProvider Instance { get; } = new FrameworkInfoProvider ();

		readonly List<IdentifierInfo> frameworks = new List<IdentifierInfo> ();
		readonly Dictionary<string,(IdentifierInfo,VersionInfo)> frameworkByShortName = new Dictionary<string,(IdentifierInfo, VersionInfo)> ();
		readonly Dictionary<string,IdentifierInfo> frameworkByMoniker = new Dictionary<string,IdentifierInfo> ();

		public FrameworkInfoProvider ()
		{
			frameworks.Add (
				new IdentifierInfo (
					".NETFramework",
					new VersionInfo (new Version (2, 0), "net20"),
					new VersionInfo (new Version (3, 0), "net30"),
					new VersionInfo (new Version (3, 5), "net35", new[] { "Client" }),
					new VersionInfo (new Version (4, 0), "net40", new[] { "Client" }),
					new VersionInfo (new Version (4, 5), "net45"),
					new VersionInfo (new Version (4, 5, 1), "net451"),
					new VersionInfo (new Version (4, 5, 2), "net452"),
					new VersionInfo (new Version (4, 6), "net46"),
					new VersionInfo (new Version (4, 6, 1), "net461"),
					new VersionInfo (new Version (4, 6, 2), "net462"),
					new VersionInfo (new Version (4, 7), "net47"),
					new VersionInfo (new Version (4, 7, 1), "net471"),
					new VersionInfo (new Version (4, 7, 2), "net472"),
					new VersionInfo (new Version (4, 8), "net48")
				)
			);

			frameworks.Add (
				new IdentifierInfo (
					".NETCoreApp",
					new VersionInfo (new Version (1, 0), "netcoreapp1.0"),
					new VersionInfo (new Version (1, 1), "netcoreapp1.1"),
					new VersionInfo (new Version (2, 0), "netcoreapp2.0"),
					new VersionInfo (new Version (2, 0), "netcoreapp2.1"),
					new VersionInfo (new Version (2, 0), "netcoreapp2.2"),
					new VersionInfo (new Version (2, 0), "netcoreapp3.0")
					new VersionInfo (new Version (3, 1), "netcoreapp3.1"),
					new VersionInfo (new Version (5, 0), "netcoreapp5.0")
				)
			);

			frameworks.Add (
				new IdentifierInfo (
					".NETStandard",
					new VersionInfo (new Version (1, 0), "netstandard1.0"),
					new VersionInfo (new Version (1, 1), "netstandard1.1"),
					new VersionInfo (new Version (1, 2), "netstandard1.2"),
					new VersionInfo (new Version (1, 3), "netstandard1.3"),
					new VersionInfo (new Version (1, 4), "netstandard1.4"),
					new VersionInfo (new Version (1, 5), "netstandard1.5"),
					new VersionInfo (new Version (1, 6), "netstandard1.6"),
					new VersionInfo (new Version (2, 0), "netstandard2.0")
				)
			);

			frameworks.Add (
				new IdentifierInfo (
					".NETPortable",
					new VersionInfo (
						new Version (4, 0), null, new [] {
							"Profile5",
							"Profile6",
							"Profile14",
							"Profile19",
							"Profile24",
							"Profile37",
							"Profile42",
							"Profile47",
							"Profile92",
							"Profile102",
							"Profile136",
							"Profile147",
							"Profile158",
							"Profile225",
							"Profile240",
							"Profile255",
							"Profile328",
							"Profile336",
							"Profile344"
						}
					),
					new VersionInfo (
						new Version (4, 5), null, new [] {
							"Profile7",
							"Profile49",
							"Profile78",
							"Profile111",
							"Profile259"
						}
					),
					new VersionInfo (
						new Version (4, 6), null, new [] {
							"Profile44",
							"Profile151"
						}
					),
					new VersionInfo (new Version (5, 0))
				)
			);

			frameworks.Add (
				new IdentifierInfo (
					"MonoAndroid",
					new VersionInfo (new Version (1, 0)),
					new VersionInfo (new Version (2, 3)),
					new VersionInfo (new Version (4, 0, 3)),
					new VersionInfo (new Version (4, 1)),
					new VersionInfo (new Version (4, 2)),
					new VersionInfo (new Version (4, 3)),
					new VersionInfo (new Version (4, 4)),
					new VersionInfo (new Version (4, 4, 87)),
					new VersionInfo (new Version (5, 0)),
					new VersionInfo (new Version (5, 1)),
					new VersionInfo (new Version (6, 0)),
					new VersionInfo (new Version (7, 0)),
					new VersionInfo (new Version (7, 1)),
					new VersionInfo (new Version (8, 0))
				)
			);

			frameworks.Add (new IdentifierInfo ("Xamarin.Mac", new VersionInfo (new Version (2, 0))));
			frameworks.Add (new IdentifierInfo ("Xamarin.TVOS", new VersionInfo (new Version (1, 0))));
			frameworks.Add (new IdentifierInfo ("Xamarin.WatchOS", new VersionInfo (new Version (1, 0))));
			frameworks.Add (new IdentifierInfo ("Xamarin.iOS", new VersionInfo (new Version (1, 0))));
			frameworks.Add (new IdentifierInfo ("MonoUE", new VersionInfo (new Version (1, 0))));

			foreach (var fx in frameworks) {
				frameworkByMoniker.Add (fx.Identifier, fx);
				foreach (var v in fx.Versions) {
					if (v.ShortName != null) {
						frameworkByShortName.Add (v.ShortName, (fx, v));
					}
				}
			}
		}

		public bool IsFrameworkShortNameValid (string shortName) => frameworkByShortName.ContainsKey (shortName);
		public bool IsFrameworkIdentifierValid (string moniker) => frameworkByMoniker.ContainsKey (moniker);

		public bool IsFrameworkVersionValid (string moniker, Version version)
			=> frameworkByMoniker.TryGetValue (moniker, out var fx) && fx.Versions.Any (v => AreVersionsEquivalent (v.Version, version));

		public bool IsFrameworkProfileValid (string moniker, Version version, string profile)
			=> frameworkByMoniker.TryGetValue (moniker, out var fx)
			&& fx.Versions.FirstOrDefault (v => AreVersionsEquivalent (v.Version, version)) is VersionInfo versionInfo
			&& versionInfo.Profiles != null && versionInfo.Profiles.Any (p => p == profile);

		public IEnumerable<FrameworkInfo> GetFrameworksWithShortNames ()
		{
			foreach (var id in frameworks) {
				foreach (var version in id.Versions) {
					if (version.ShortName != null) {
						yield return new FrameworkInfo (version.ShortName, id.Identifier, version.Version, null);
					}
				}
			}
		}

		public IEnumerable<FrameworkInfo> GetFrameworkIdentifiers ()
		{
			foreach (var fx in frameworks) {
				yield return new FrameworkInfo (fx.Identifier, fx.Identifier, null, null);
			}
		}

		public IEnumerable<FrameworkInfo> GetFrameworkVersions (string identifier)
		{
			foreach (var id in frameworks) {
				if (string.Equals (id.Identifier, identifier, StringComparison.OrdinalIgnoreCase)) {
					foreach (var v in id.Versions) {
						yield return new FrameworkInfo ("v" + FormatDisplayVersion (v.Version), id.Identifier, v.Version, null);
					}
				}
			}
		}

		public IEnumerable<FrameworkInfo> GetFrameworkProfiles (string identifier, Version version)
		{
			foreach (var id in frameworks) {
				if (!string.Equals (id.Identifier, identifier, StringComparison.OrdinalIgnoreCase)) {
					continue;
				}
				foreach (var v in id.Versions) {
					if (v.Version != version) {
						continue;
					}
					foreach (var p in v.Profiles) {
						yield return new FrameworkInfo (p, id.Identifier, v.Version, p);
					}
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
				return WithVersion (".NET Core App");
			case ".netportable":
				if (fx.Profile == null || fx.Version == null) {
					return "Portable Class Library";
				}
				return $"Portable Class Library Profile {fx.Profile}";
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
				if (fx.Version == null) {
					return description;
				}
				return $"{description} {FormatDisplayVersion (fx.Version)}";
			}
		}

        class IdentifierInfo
		{
			public IdentifierInfo (string identifier, params VersionInfo [] versions)
			{
				Identifier = identifier;
				Versions = versions;
			}

			public string Identifier { get; }
			public VersionInfo [] Versions { get; }
		}

		class VersionInfo
		{
			public VersionInfo (Version version, string shortname = null, string [] profiles = null)
			{
				Version = version;
				ShortName = shortname;
				Profiles = profiles;
			}

			public Version Version { get; }
			public string ShortName { get; }
			public string [] Profiles { get; }
		}
	}

	// this is kinda weird as it can represent a value that's a piece of a framework ID:
	// a shortname, identifier, version or profile
	// the "name" is the piece that's being represented and the reference is the
	// full ID, or as close to it as we have
	class FrameworkInfo : BaseInfo
	{
		public FrameworkInfo (string name, NuGetFramework reference)
			: base (name, null)
		{
			Reference = reference;
		}

		public FrameworkInfo (string name, string identifier, Version version, string profile)
			: this (name, new NuGetFramework (identifier, version, profile))
		{
		}

		public NuGetFramework Reference { get; }
	}
}
