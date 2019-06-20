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
		public string GetBinPath () => null;
		public IEnumerable<string> GetExtensionsPaths () => Enumerable.Empty<string> ();
		public IList<SdkInfo> GetRegisteredSdks () => Array.Empty<SdkInfo> ();
		public string GetSdkPath (SdkReference sdk, string projectFile, string solutionPath) => null;
		public string GetSdksPath () => null;
		public string GetToolsPath () => null;
	}
}