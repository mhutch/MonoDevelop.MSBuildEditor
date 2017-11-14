// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MonoDevelop.Core;
using NuGet.Protocol.Core.Types;
using ProjectFileTools.NuGetSearch.Contracts;

namespace MonoDevelop.MSBuildEditor.PackageSearch
{
	class MonoDevelopPackageFeedRegistry : IPackageFeedRegistryProvider
	{
		public MonoDevelopPackageFeedRegistry ()
		{
			var sources = new List<string> ();

			try {
				var asm = typeof (PackageManagement.PackageManagementServices).Assembly;
				var sourceFactoryType = asm.GetType ("MonoDevelop.PackageManagement.SourceRepositoryProviderFactory");
				var createProvider = sourceFactoryType.GetMethod ("CreateSourceRepositoryProvider", new Type[0]);

				var provider = (ISourceRepositoryProvider)createProvider.Invoke (null, null);
				var providerSources = provider.PackageSourceProvider.LoadPackageSources ();

				foreach (var source in providerSources) {
					sources.Add (source.Source);
				}
			} catch (Exception ex) {
				LoggingService.LogError ("Failed to get configured NuGet feeds", ex);
			}

			if (!sources.Any (x => x.IndexOf (Path.DirectorySeparatorChar + ".nuget", StringComparison.OrdinalIgnoreCase) > -1)) {
				var homeDir = Environment.GetFolderPath (Environment.SpecialFolder.Personal);
				sources.Add (Path.Combine (homeDir, ".nuget", "packages"));
			}

			ConfiguredFeeds = sources;
		}

		public IReadOnlyList<string> ConfiguredFeeds { get; private set; }
	}
}