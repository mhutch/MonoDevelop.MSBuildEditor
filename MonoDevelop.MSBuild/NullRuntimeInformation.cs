// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.SdkResolution;

namespace MonoDevelop.MSBuild.Editor.Completion
{
	class NullRuntimeInformation : IRuntimeInformation
	{
		public string ToolsVersion => MSBuildToolsVersion.Unknown.ToVersionString ();

		public string BinPath => throw new NotImplementedException ();

		public string ToolsPath => throw new NotImplementedException ();

		public IReadOnlyDictionary<string, IReadOnlyList<string>> SearchPaths { get; } = new Dictionary<string, IReadOnlyList<string>> ();

		public string SdksPath => null;

		public IList<SdkInfo> GetRegisteredSdks () => Array.Empty<SdkResolution.SdkInfo> ();

		public SdkInfo ResolveSdk (
			(string name, string version, string minimumVersion) sdk, string projectFile, string solutionPath
			) => null;
	}
}