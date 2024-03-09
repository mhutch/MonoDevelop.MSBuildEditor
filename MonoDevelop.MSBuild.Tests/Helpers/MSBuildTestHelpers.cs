// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;

using MonoDevelop.MSBuild.Util;

namespace MonoDevelop.MSBuild.Tests.Helpers;

static class MSBuildTestHelpers
{
	static bool registeredAssemblies;

	public static void RegisterMSBuildAssemblies ()
	{
		if (registeredAssemblies) {
			return;
		}
		registeredAssemblies = true;

#if NETFRAMEWORK
		if (Platform.IsWindows) {
			var vs17Instance = Microsoft.Build.Locator.MSBuildLocator.QueryVisualStudioInstances ()
				.FirstOrDefault (x => x.DiscoveryType == Microsoft.Build.Locator.DiscoveryType.VisualStudioSetup && x.Version.Major >= 17);
			if (vs17Instance == null) {
				throw new InvalidOperationException ("Did not find instance of Visual Studio 17.0 or later");
			}
			Microsoft.Build.Locator.MSBuildLocator.RegisterInstance (vs17Instance);
			return;
		}
#endif
		var dotnetInstance = Microsoft.Build.Locator.MSBuildLocator.QueryVisualStudioInstances ()
			.FirstOrDefault (x => x.DiscoveryType == Microsoft.Build.Locator.DiscoveryType.DotNetSdk && x.Version.Major >= 6.0);
		if (dotnetInstance == null) {
			throw new InvalidOperationException ("Did not find instance of .NET 6.0 or later");
		}
		Microsoft.Build.Locator.MSBuildLocator.RegisterInstance (dotnetInstance);
		return;
	}

	/*
	// might need this again, keep it around for now
	void FindMSBuildInPath ()
	{
		var msbuildInPath = FindInPath ("msbuild");
		if (msbuildInPath != null) {
			//attempt to read the msbuild.dll location from the launch script
			//FIXME: handle quoting in the script
			Console.WriteLine ("Found msbuild script in PATH: {0}", msbuildInPath);
			var tokens = File.ReadAllText (msbuildInPath).Split (new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
			var filename = tokens.FirstOrDefault (t => t.EndsWith ("MSBuild.dll", StringComparison.OrdinalIgnoreCase));
			if (filename != null && File.Exists (filename)) {
				var dir = Path.GetDirectoryName (filename);
				Microsoft.Build.Locator.MSBuildLocator.RegisterMSBuildPath (dir);
				Console.WriteLine ("Discovered MSBuild from launch script: {0}", dir);
				return;
			}
		}

		foreach (var dir in GetPossibleMSBuildDirectoriesLinux ()) {
			if (File.Exists (Path.Combine (dir, "MSBuild.dll"))) {
				Microsoft.Build.Locator.MSBuildLocator.RegisterMSBuildPath (dir);
				Console.WriteLine ("Discovered MSBuild at well known location: {0}", dir);
				return;
			}
		}

		throw new Exception ("Could not find MSBuild");
	}

	static IEnumerable<string> GetPossibleMSBuildDirectoriesLinux ()
	{
		yield return "/usr/lib/mono/msbuild/Current/bin";
		yield return "/usr/lib/mono/msbuild/15.0/bin";
	}

	static string FindInPath (string name)
	{
		var pathEnv = Environment.GetEnvironmentVariable ("PATH");
		if (pathEnv == null) {
			return null;
		}

		var paths = pathEnv.Split (new[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries);
		foreach (var path in paths) {
			var possible = Path.Combine (path, name);
			if (File.Exists (possible)) {
				return possible;
			}
		}

		return null;
	}
	*/
}
