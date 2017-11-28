// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using MonoDevelop.Core;
using MonoDevelop.Core.Assemblies;
using MonoDevelop.MSBuildEditor.Language;

namespace MonoDevelop.MSBuildEditor
{
	class MSBuildRuntimeInformation : IRuntimeInformation
	{
		TargetRuntime target;
		MSBuildSdkResolver sdkResolver;
		string tvString;

		public MSBuildRuntimeInformation (TargetRuntime target, MSBuildToolsVersion tv)
		{
			this.target = target;
			this.sdkResolver = new MSBuildSdkResolver (target);
			this.tvString = tv.ToVersionString ();
		}

		public string GetBinPath () => target.GetMSBuildBinPath (tvString);

		public string GetToolsPath () => target.GetMSBuildToolsPath (tvString);

		public IEnumerable<string> GetExtensionsPaths ()
		{
			yield return target.GetMSBuildExtensionsPath ();
			if (Platform.IsMac) {
				yield return "/Library/Frameworks/Mono.framework/External/xbuild";
			}
		}

		public string GetSdksPath () => sdkResolver.DefaultSdkPath;

		public List<MSBuildSdkResolver.SdkInfo> GetRegisteredSdks () => sdkResolver.GetRegisteredSdks ();

		public string GetSdkPath (Microsoft.Build.Framework.SdkReference sdk, string projectFile, string solutionPath)
			=> sdkResolver.GetSdkPath (sdk, projectFile, solutionPath);
	}
}
