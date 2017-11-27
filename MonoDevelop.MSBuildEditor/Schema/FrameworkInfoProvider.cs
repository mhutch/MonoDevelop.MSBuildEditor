// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace MonoDevelop.MSBuildEditor.Schema
{
	// We can't rely on checking the system or the host IDE for frameworks, as they
	// may not be installed. We also can't use NuGet.Frameworks, as it has a rather skewed
	// worldview and sometimes omits important things like version numbers
	class FrameworkInfoProvider
	{
		public static FrameworkInfoProvider Instance { get; } = new FrameworkInfoProvider ();

		List<IdentifierInfo> frameworks = new List<IdentifierInfo> ();

		public FrameworkInfoProvider ()
		{
			frameworks.Add (
				new IdentifierInfo (
					".NETFramework",
					new VersionInfo ("v2.0", "net20"),
					new VersionInfo ("v3.0", "net30"),
					new VersionInfo ("v3.5", "net35"),
					new VersionInfo ("v4.0", "net40"),
					new VersionInfo ("v4.5", "net45"),
					new VersionInfo ("v4.5.1", "net451"),
					new VersionInfo ("v4.5.2", "net452"),
					new VersionInfo ("v4.6", "net46"),
					new VersionInfo ("v4.6.1", "net461"),
					new VersionInfo ("v4.6.2", "net462"),
					new VersionInfo ("v4.7", "net47"),
					new VersionInfo ("v4.7.1", "net471")
				)
			);

			frameworks.Add (
				new IdentifierInfo (
					".NETCoreApp",
					new VersionInfo ("v1.0", "netcoreapp1.0"),
					new VersionInfo ("v1.1", "netcoreapp1.1"),
					new VersionInfo ("v2.0", "netcoreapp2.0")
				)
			);

			frameworks.Add (
				new IdentifierInfo (
					".NETStandard",
					new VersionInfo ("v1.0", "netstandard1.0"),
					new VersionInfo ("v1.1", "netstandard1.1"),
					new VersionInfo ("v1.2", "netstandard1.2"),
					new VersionInfo ("v1.3", "netstandard1.3"),
					new VersionInfo ("v1.4", "netstandard1.4"),
					new VersionInfo ("v1.5", "netstandard1.5"),
					new VersionInfo ("v1.6", "netstandard1.6"),
					new VersionInfo ("v2.0", "netstandard2.0")
				)
			);

			frameworks.Add (
				new IdentifierInfo (
					".NETPortable",
					new VersionInfo (
						"v4.0", null, new [] {
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
						"v4.5", null, new [] {
							"Profile7",
							"Profile49",
							"Profile78",
							"Profile111",
							"Profile259"
						}
					),
					new VersionInfo (
						"v4.6", null, new [] {
							"Profile44",
							"Profile151"
						}
					)
				)
			);

			frameworks.Add (new IdentifierInfo (".NETPortable", new VersionInfo ("v5.0")));

			frameworks.Add (
				new IdentifierInfo (
					"MonoAndroid",
					new VersionInfo ("v1.0"),
					new VersionInfo ("v2.3"),
					new VersionInfo ("v4.0.3"),
					new VersionInfo ("v4.1"),
					new VersionInfo ("v4.2"),
					new VersionInfo ("v4.3"),
					new VersionInfo ("v4.4"),
					new VersionInfo ("v4.4.87"),
					new VersionInfo ("v5.0"),
					new VersionInfo ("v5.1"),
					new VersionInfo ("v6.0"),
					new VersionInfo ("v7.0"),
					new VersionInfo ("v7.1"),
					new VersionInfo ("v8.0")
				)
			);

			frameworks.Add (new IdentifierInfo ("Xamarin.Mac", new VersionInfo ("v2.0")));
			frameworks.Add (new IdentifierInfo ("Xamarin.TVOS", new VersionInfo ("v1.0")));
			frameworks.Add (new IdentifierInfo ("Xamarin.WatchOS", new VersionInfo ("v1.0")));
			frameworks.Add (new IdentifierInfo ("Xamarin.iOS", new VersionInfo ("v1.0")));
			frameworks.Add (new IdentifierInfo ("MonoUE", new VersionInfo ("v1.0")));
		}

		public IEnumerable<FrameworkInfo> GetFrameworksWithShortNames ()
		{
			foreach (var id in frameworks) {
				foreach (var version in id.Versions) {
					if (version.ShortName != null) {
						yield return new FrameworkInfo (version.ShortName, id.Identifier, version.Version, version.ShortName, null);
					}
				}
			}
		}

		public IEnumerable<FrameworkInfo> GetFrameworkIdentifiers ()
		{
			foreach (var fx in frameworks) {
				yield return new FrameworkInfo (fx.Identifier, fx.Identifier, null, null, null);
			}
		}

		public IEnumerable<FrameworkInfo> GetFrameworkVersions (string identifier)
		{
			foreach (var id in frameworks) {
				if (string.Equals (id.Identifier, identifier, StringComparison.OrdinalIgnoreCase)) {
					foreach (var v in id.Versions) {
						yield return new FrameworkInfo (v.Version, id.Identifier, v.Version, v.ShortName, null);
					}
				}
			}
		}

		public IEnumerable<FrameworkInfo> GetFrameworkProfiles (string identifier, string version)
		{
			if (version.Length > 0 && version [0] != 'v' && version [0] != 'V') {
				version = "v" + version;
			}
			foreach (var id in frameworks) {
				if (string.Equals (id.Identifier, identifier, StringComparison.OrdinalIgnoreCase)) {
					foreach (var v in id.Versions) {
						foreach (var p in v.Profiles) {
							yield return new FrameworkInfo (p, id.Identifier, v.Version, null, p);
						}
					}
				}
			}
		}

		public static string GetDescription (FrameworkReference fx)
		{
			switch (fx.Identifier.ToLowerInvariant ()) {
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
				return $"{description} {fx.Version.TrimStart ('v')}";
			}

		}

        internal BaseInfo GetBestInfo (FrameworkReference fx, IReadOnlyList<FrameworkReference> tfms)
        {
			if (fx.ShortName != null) {
				var fullref = FrameworkReference.FromShortName (fx.ShortName);
				return new FrameworkInfo (fx.ShortName, fullref);
			}

			if (fx.Identifier != null) {
				return new FrameworkInfo (fx.Identifier, fx);
			}

			if (fx.Version != null) {
				foreach (var tfm in tfms) {
					foreach (var f in GetFrameworkVersions (tfm.Identifier)) {
						if (string.Equals (f.Name, fx.Version, StringComparison.OrdinalIgnoreCase)) {
							return f;
						}
					}
				}
			}

			if (fx.Profile != null) {
				foreach (var tfm in tfms) {
					foreach (var f in GetFrameworkProfiles (tfm.Identifier, tfm.Version)) {
						if (string.Equals (f.Name, fx.Profile, StringComparison.OrdinalIgnoreCase)) {
							return f;
						}
					}
				}
			}
			return null;
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
			public VersionInfo (string version, string shortname = null, string [] profiles = null)
			{
				Version = version;
				ShortName = shortname;
				Profiles = profiles;
			}

			public string Version { get; }
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
		public FrameworkInfo (string name, FrameworkReference reference)
			: base (name, null)
		{
			Reference = reference;
		}

		public FrameworkInfo (string name, string identifier, string version, string shortName, string profile)
			: this (name, new FrameworkReference (identifier, version, shortName, profile))
		{
		}

		public FrameworkReference Reference { get; }
	}

	class FrameworkReference
	{
		public FrameworkReference (string identifier, string version, string shortName, string profile)
		{
			Identifier = identifier;
			Version = version;
			Profile = profile;
			ShortName = shortName;
		}

		public string Identifier { get; }
		public string Version { get; }
		public string Profile { get; }
		public string ShortName { get; }

		public string GetMoniker ()
		{
			if (Profile != null) {
				return $"{Identifier},Version={Version},Profile={Profile}";
			}
			if (Version != null) {
				return $"{Identifier},Version={Version}";
			}
			return Identifier;
		}

		public static FrameworkReference FromShortName (string shortName)
		{
			var fx = NuGet.Frameworks.NuGetFramework.ParseFolder (shortName);
			if (fx.IsSpecificFramework) {
				var parsed = Core.Assemblies.TargetFrameworkMoniker.Parse (fx.DotNetFrameworkName);
				var profile = string.IsNullOrEmpty (fx.Profile)? null: fx.Profile;
				return new FrameworkReference (parsed.Identifier, parsed.Version, shortName, profile);
			}
			return null;
		}
	}
}
