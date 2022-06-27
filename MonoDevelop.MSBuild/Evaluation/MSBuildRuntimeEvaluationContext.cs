// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
		readonly Dictionary<string, EvaluatedValue> values = new (StringComparer.OrdinalIgnoreCase);
		readonly Dictionary<string, OneOrMany<EvaluatedValue>> projectSearchPathValues = new (StringComparer.OrdinalIgnoreCase);

		readonly IMSBuildEnvironment env;

		public MSBuildRuntimeEvaluationContext (IMSBuildEnvironment env)
		{
			this.env = env;

			if (env is NullMSBuildEnvironment) {
				return;
			}

			if (!env.ToolsetProperties.ContainsKey (ReservedProperties.BinPath)) {
				values[ReservedProperties.BinPath] = EvaluatedValue.FromNativePath (env.ToolsPath);
			}
			if (!env.ToolsetProperties.ContainsKey (ReservedProperties.ToolsPath)) {
				values[ReservedProperties.ToolsPath] = values[ReservedProperties.BinPath];
			}
			if (!env.ToolsetProperties.ContainsKey (ReservedProperties.ToolsVersion)) {
				values[ReservedProperties.ToolsVersion] = new (env.ToolsVersion);
			}
			if (!env.ToolsetProperties.ContainsKey (ReservedProperties.VisualStudioVersion)) {
				values[ReservedProperties.VisualStudioVersion] = new ("17.0");
			}
			if (!env.ToolsetProperties.ContainsKey (ReservedProperties.ProgramFiles32)) {
				values[ReservedProperties.ProgramFiles32] = EvaluatedValue.FromNativePath (Environment.GetFolderPath (Environment.SpecialFolder.ProgramFilesX86));
			}
			if (!env.ToolsetProperties.ContainsKey (ReservedProperties.ProgramFiles64)) {
				values[ReservedProperties.ProgramFiles64] = EvaluatedValue.FromNativePath (Environment.GetFolderPath (Environment.SpecialFolder.ProgramFiles));
			}
		}

		public bool TryGetProperty (string name, [NotNullWhen (true)] out EvaluatedValue? value)
		{
			if (values.TryGetValue (name, out var existingVal)) {
				value = existingVal;
				return true;
			}

			if (env.ToolsetProperties.TryGetValue (name, out var toolsetValue)) {
				var escapedValue = EvaluatedValue.FromNativePath (toolsetValue);
				value = escapedValue;
				values[name] = escapedValue;
				return true;
			}

			value = null;
			return false;
		}

		public bool TryGetMultivaluedProperty (string name, [NotNullWhen (true)] out OneOrMany<EvaluatedValue>? value, bool isProjectImportStart = false)
		{
			if (isProjectImportStart) {
				if (projectSearchPathValues.TryGetValue (name, out var pathValues)) {
					value = pathValues;
					return true;
				}

				if (env.ProjectImportSearchPaths.TryGetValue (name, out var paths)) {
					//FIXME do we need to add in the values from the toolset props?
				}
			}

			if (TryGetProperty (name, out var singleValue)) {
				value = singleValue;
				return true;
			}

			value = null;
			return false;
		}
	}
}