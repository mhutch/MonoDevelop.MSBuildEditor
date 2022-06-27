// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using MonoDevelop.MSBuild.SdkResolution;

namespace MonoDevelop.MSBuild
{
	/// <summary>Describes an MSBuild installation</summary>
	public interface IMSBuildEnvironment
	{
		Version EngineVersion { get; }
		string ToolsVersion { get; }
		string ToolsPath { get; }

		/// <summary>
		/// Properties defined by the environment, such as MSBuildExtensionsPaths
		/// </summary>
		IReadOnlyDictionary<string, string> ToolsetProperties { get; }

		/// <summary>
		/// Multivalued fallback properties for use only when evaluating the first property in Import elements. Defined in projectImportSearchPaths in app.config.
		/// </summary>
		IReadOnlyDictionary<string, string[]> ProjectImportSearchPaths { get; }

		IList<SdkInfo> GetRegisteredSdks ();

		//NOTE: we don't use SdkReference so as not to expose Microsoft.Build API publically
		//as that causes issues in VSMac's unit test discovery
		SdkInfo ResolveSdk ((string name, string version, string minimumVersion) sdk, string projectFile, string solutionPath);
	}

	public static class MSBuildEnvironmentExtensions
	{
		public static IEnumerable<string> EnumerateFilesInToolsPath (this IMSBuildEnvironment env, string searchPattern)
		{
			if (env.ToolsPath == null) {
				return Enumerable.Empty<string> ();
			}
			return Directory.EnumerateFiles (env.ToolsPath, searchPattern);
		}
	}
}
