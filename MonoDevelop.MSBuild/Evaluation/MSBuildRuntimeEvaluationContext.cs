// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Util;

namespace MonoDevelop.MSBuild.Evaluation
{
	/// <summary>
	/// Provides MSBuild properties built into the runtime itself
	/// </summary>
	class MSBuildRuntimeEvaluationContext : IMSBuildEvaluationContext
	{
		readonly Dictionary<string, MSBuildPropertyValue> values
			= new Dictionary<string, MSBuildPropertyValue> (StringComparer.OrdinalIgnoreCase);

		public MSBuildRuntimeEvaluationContext (IRuntimeInformation runtime)
		{
			string binPath = MSBuildEscaping.ToMSBuildPath (runtime.BinPath);
			string toolsPath = MSBuildEscaping.ToMSBuildPath (runtime.ToolsPath);

			Convert ("MSBuildExtensionsPath");
			Convert ("MSBuildExtensionsPath32");
			Convert ("MSBuildExtensionsPath64");

			void Convert (string name)
			{
				if (runtime.SearchPaths.TryGetValue (name, out var vals)) {
					values[name] = new MSBuildPropertyValue (vals.ToArray ());
				}
			}

			values["MSBuildBinPath"] = binPath;
			values["MSBuildToolsPath"] = toolsPath;
			values["MSBuildToolsPath32"] = toolsPath;
			values["MSBuildToolsPath64"] = toolsPath;
			values["RoslynTargetsPath"] = $"{binPath}\\Roslyn";
			values["MSBuildToolsVersion"] = runtime.ToolsVersion;
			values["VisualStudioVersion"] = "15.0";

			values["MSBuildProgramFiles32"] = MSBuildEscaping.ToMSBuildPath (Environment.GetFolderPath (Environment.SpecialFolder.ProgramFilesX86));
			values["MSBuildProgramFiles64"] = MSBuildEscaping.ToMSBuildPath (Environment.GetFolderPath (Environment.SpecialFolder.ProgramFiles));

			var defaultSdksPath = runtime.SdksPath;
			if (defaultSdksPath != null) {
				values["MSBuildSDKsPath"] = MSBuildEscaping.ToMSBuildPath (defaultSdksPath, null);
			}

			//these are read from a config file and may be described in terms of other properties
			values["MSBuildExtensionsPath"] = values["MSBuildExtensionsPath"].Collapse (this);
			values["MSBuildExtensionsPath32"] = values["MSBuildExtensionsPath32"].Collapse (this);
			values["MSBuildExtensionsPath64"] = values["MSBuildExtensionsPath64"].Collapse (this);
		}

		public bool TryGetProperty (string name, out MSBuildPropertyValue value) => values.TryGetValue (name, out value);
	}
}