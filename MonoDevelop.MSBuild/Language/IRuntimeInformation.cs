// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace MonoDevelop.MSBuild.Language
{
	interface IRuntimeInformation
	{
		string GetBinPath ();
		string GetToolsPath ();
		IReadOnlyDictionary<string, IReadOnlyList<string>> GetSearchPaths ();
		string GetSdksPath ();
		IList<SdkInfo> GetRegisteredSdks ();
		string GetSdkPath (Microsoft.Build.Framework.SdkReference sdk, string projectFile, string solutionPath);
	}
}
