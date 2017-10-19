//
// Copyright (c) 2017 Microsoft Corp.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MonoDevelop.Core;
using NuGet.Protocol.Core.Types;
using ProjectFileTools.NuGetSearch.Contracts;

namespace MonoDevelop.MSBuildEditor
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