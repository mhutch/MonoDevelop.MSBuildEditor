// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.Build.Framework;
using MonoDevelop.Core;
using MonoDevelop.Core.Assemblies;
using MonoDevelop.MSBuild;
using MonoDevelop.MSBuild.SdkResolution;
using MonoDevelop.Projects.MSBuild;
using LoggingService = MonoDevelop.Core.LoggingService;

namespace MonoDevelop.MSBuildEditor
{
	[Export (typeof (IMSBuildEnvironment))]
	class MonoDevelopMSBuildEnvironment : IMSBuildEnvironment
	{
		readonly MonoDevelopSdkResolver sdkResolver;
		Dictionary<SdkReference, SdkResult> resolvedSdks = new Dictionary<SdkReference, SdkResult> ();

		public MonoDevelopMSBuildEnvironment ()
		{
			var runtime = Runtime.SystemAssemblyService.DefaultRuntime;
			ToolsVersion = "17.0";
			EngineVersion = runtime.GetMSBuildVersion(ToolsVersion);
			ToolsPath = runtime.GetMSBuildBinPath (ToolsVersion);

			sdkResolver = new MonoDevelopSdkResolver (runtime);

			var extensionPaths = GetExtensionsPaths (runtime).ToArray ();
			ProjectImportSearchPaths = new Dictionary<string, string[]> {
				{ "MSBuildExtensionsPath", extensionPaths },
				{ "MSBuildExtensionsPath32", extensionPaths },
				{ "MSBuildExtensionsPath64", extensionPaths }
			};

			ToolsetProperties = new Dictionary<string, string> {
				// FIXME: VALUES
			};
		}

		IEnumerable<string> GetExtensionsPaths (TargetRuntime runtime)
		{
			yield return runtime.GetMSBuildExtensionsPath ();
			if (Platform.IsMac) {
				yield return "/Library/Frameworks/Mono.framework/External/xbuild";
			}
		}

		public Version EngineVersion { get; }

		public string ToolsVersion { get; }

		public string ToolsPath { get; }

		public IReadOnlyDictionary<string, string[]> ProjectImportSearchPaths { get; }

		public IReadOnlyDictionary<string, string> ToolsetProperties { get; }

		public IList<SdkInfo> GetRegisteredSdks () => sdkResolver.GetRegisteredSdks ();

		//FIXME: caching should be specific to the (projectFile, string solutionPath) pair
		public SdkInfo ResolveSdk ((string name, string version, string minimumVersion) sdk, string projectFile, string solutionPath)
		{
			var sdkRef = new SdkReference (sdk.name, sdk.version, sdk.minimumVersion);
			if (!resolvedSdks.TryGetValue (sdkRef, out SdkResult result)) {
				try {
					result = sdkResolver.Resolve (sdkRef, projectFile, solutionPath);
				} catch (Exception ex) {
					LoggingService.LogError ("Error in SDK resolver", ex);
				}
				resolvedSdks[sdkRef] = result;
			}
			return new SdkInfo (sdk.name, result);
		}
	}
}
