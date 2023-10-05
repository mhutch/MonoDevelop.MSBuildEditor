// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;

using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.SdkResolution;

namespace MonoDevelop.MSBuild
{
	class NullMSBuildEnvironment : IMSBuildEnvironment
	{
		public Version EngineVersion => new(0, 0);

		public string ToolsVersion => MSBuildToolsVersion.Unknown.ToVersionString ();

		public string ToolsPath => null;

		public IReadOnlyDictionary<string, string> ToolsetProperties { get; } = new Dictionary<string, string> ();

		public IReadOnlyDictionary<string, string> EnvironmentVariables { get; } = new Dictionary<string, string> ();

		public IReadOnlyDictionary<string, string[]> ProjectImportSearchPaths { get; } = new Dictionary<string, string[]> ();

		public IList<SdkInfo> GetRegisteredSdks () => Array.Empty<SdkInfo> ();

		public SdkInfo ResolveSdk ((string name, string version, string minimumVersion) sdk, string projectFile, string solutionPath, ILogger logger) => null;
	}
}