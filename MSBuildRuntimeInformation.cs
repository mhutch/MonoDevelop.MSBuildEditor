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

		public MSBuildRuntimeInformation (TargetRuntime target, MSBuildToolsVersion tv, MSBuildSdkResolver sdkResolver)
		{
			this.target = target;
			this.sdkResolver = sdkResolver;
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

	}
}
