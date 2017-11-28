// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace MonoDevelop.MSBuildEditor.Language
{
	interface IRuntimeInformation
	{
		string GetBinPath ();
		string GetToolsPath ();
		IEnumerable<string> GetExtensionsPaths ();
		string GetSdksPath ();
		List<MSBuildSdkResolver.SdkInfo> GetRegisteredSdks ();
		string GetSdkPath (Microsoft.Build.Framework.SdkReference sdk, string projectFile, string solutionPath);

	}
}
