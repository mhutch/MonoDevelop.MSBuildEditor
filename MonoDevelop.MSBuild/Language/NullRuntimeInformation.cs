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
		public string ToolsVersion => MSBuildToolsVersion.Unknown.ToVersionString ();

		public string BinPath => throw new NotImplementedException ();

		public string ToolsPath => throw new NotImplementedException ();

		public IReadOnlyDictionary<string, IReadOnlyList<string>> SearchPaths { get; } = new Dictionary<string, IReadOnlyList<string>> ();

		public string SdksPath => null;

		public IList<SdkInfo> GetRegisteredSdks () => Array.Empty<SdkInfo> ();

		public string GetSdkPath (SdkReference sdk, string projectFile, string solutionPath) => null;
	}
}