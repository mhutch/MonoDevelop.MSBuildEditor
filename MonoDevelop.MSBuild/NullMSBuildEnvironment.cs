// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.SdkResolution;

namespace MonoDevelop.MSBuild.Editor.Completion
{
	class NullMSBuildEnvironment : IMSBuildEnvironment
	{
		public Version EngineVersion => new(0, 0);

		public string ToolsVersion => MSBuildToolsVersion.Unknown.ToVersionString ();

		public string ToolsPath => null;

		public bool TryGetToolsetProperty (string propertyName, out string value)
		{
			value = null;
			return false;
		}

		public IReadOnlyDictionary<string, IReadOnlyList<string>> SearchPaths { get; } = new Dictionary<string, IReadOnlyList<string>> ();

		public IList<SdkInfo> GetRegisteredSdks () => Array.Empty<SdkInfo> ();

		public SdkInfo ResolveSdk (
			(string name, string version, string minimumVersion) sdk, string projectFile, string solutionPath
			) => null;
	}
}