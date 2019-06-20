// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using MonoDevelop.MSBuild.Language;

namespace MonoDevelop.MSBuild.Editor.Completion
{
	class NullRuntimeInformation : IRuntimeInformation
	{
		Dictionary<string, IReadOnlyList<string>> searchPaths = new Dictionary<string, IReadOnlyList<string>>();

		public string GetBinPath () => null;
		public IList<SdkInfo> GetRegisteredSdks () => Array.Empty<SdkInfo> ();
		public string GetSdkPath (SdkReference sdk, string projectFile, string solutionPath) => null;
		public string GetSdksPath () => null;

		public IReadOnlyDictionary<string, IReadOnlyList<string>> GetSearchPaths () => searchPaths;

		public string GetToolsPath () => null;
	}
}