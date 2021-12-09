// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using MonoDevelop.MSBuild.Language.Expressions;
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

		readonly IMSBuildEnvironment env;

		public MSBuildRuntimeEvaluationContext (IMSBuildEnvironment env)
		{
			this.env = env;

			if (env is Editor.Completion.NullMSBuildEnvironment) {
				return;
			}

			string toolsPath = MSBuildEscaping.ToMSBuildPath (env.ToolsPath);

			values[ReservedProperties.BinPath] = toolsPath;
			values[ReservedProperties.ToolsPath] = toolsPath;

			ConvertSearchPaths (ReservedProperties.ExtensionsPath);
			ConvertSearchPaths (ReservedProperties.ExtensionsPath32);
			ConvertSearchPaths (ReservedProperties.ExtensionsPath64);

			void ConvertSearchPaths (string name)
			{
				if (env.SearchPaths.TryGetValue (name, out var vals)) {
					values[name] = new MSBuildPropertyValue (vals.Select (v => ExpressionParser.Parse (v, ExpressionOptions.ItemsMetadataAndLists)).ToArray ());
				}
			}

			values[ReservedProperties.ToolsVersion] = env.ToolsVersion;
			values[ReservedProperties.VisualStudioVersion] = "17.0";

			values[ReservedProperties.ProgramFiles32] = MSBuildEscaping.ToMSBuildPath (Environment.GetFolderPath (Environment.SpecialFolder.ProgramFilesX86));
			values[ReservedProperties.ProgramFiles64] = MSBuildEscaping.ToMSBuildPath (Environment.GetFolderPath (Environment.SpecialFolder.ProgramFiles));
		}

		public bool TryGetProperty (string name, out MSBuildPropertyValue? value)
		{
			if (values.TryGetValue(name, out var existingVal)) {
				value = existingVal;
				return true;
			}

			if (env.TryGetToolsetProperty (name, out var propVal) && propVal is not null) {
				var escPropVal = MSBuildEscaping.ToMSBuildPath (propVal);
				values[name] = escPropVal;
				value = escPropVal;

				return true;
			}

			value = default;
			return false;
		}
	}
}