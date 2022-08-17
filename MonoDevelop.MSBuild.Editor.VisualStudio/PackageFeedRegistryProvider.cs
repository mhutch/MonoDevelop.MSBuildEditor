using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

using Microsoft.VisualStudio.Utilities;

using ProjectFileTools.NuGetSearch.Contracts;

using NuGet.VisualStudio;

namespace MonoDevelop.MSBuild.Editor.VisualStudio
{
	[Export (typeof (IPackageFeedRegistryProvider))]
	[Name ("Visual Studio Package Feed Registry Provider")]
	class PackageFeedRegistryProvider : IPackageFeedRegistryProvider
	{
		readonly IVsPackageSourceProvider provider;

		[ImportingConstructor]
		public PackageFeedRegistryProvider (IVsPackageSourceProvider provider)
		{
			this.provider = provider;
		}

		public IReadOnlyList<string> ConfiguredFeeds {
			get {
				var sources = new List<string> ();

				// IVsPackageSourceProvider seems to be broken in 17.3 previews so add a fallback
				try {
					IEnumerable<KeyValuePair<string, string>> enabledSources = provider.GetSources (true, false);

					foreach (KeyValuePair<string, string> curEnabledSource in enabledSources) {
						string source = curEnabledSource.Value;
						sources.Add (source);
					}
				} catch (Exception ex) {
					LoggingService.LogError ("Failed to get configured NuGet sources", ex);
					sources.Add ("https://api.nuget.org/v3/index.json");
				}

				if (!sources.Any (x => x.IndexOf ("\\.nuget", StringComparison.OrdinalIgnoreCase) > -1)) {
					sources.Add (Environment.ExpandEnvironmentVariables ("%USERPROFILE%\\.nuget\\packages"));
				}

				return sources;
			}
		}
	}
}
