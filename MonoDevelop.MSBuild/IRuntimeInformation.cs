// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace MonoDevelop.MSBuild
{
	public interface IRuntimeInformation
	{
		string ToolsVersion { get; }
		string BinPath { get; }
		string ToolsPath { get; }
		IReadOnlyDictionary<string, IReadOnlyList<string>> SearchPaths { get; }
		string SdksPath { get; }
		IList<SdkResolution.SdkInfo> GetRegisteredSdks ();
		//NOTE: we don't use SdkReference so as not to expose Microsoft.Build APi publically
		//as that causes issues in VSMac's unit test discovery
		string GetSdkPath ((string name, string version, string minimumVersion) sdk, string projectFile, string solutionPath);
	}
}
