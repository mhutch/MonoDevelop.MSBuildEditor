// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP
#nullable enable
#endif

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;

using Microsoft.Build.Shared;
using Microsoft.Extensions.Logging;

using ReservedPropertyNames = Microsoft.Build.Internal.ReservedPropertyNames;

namespace MonoDevelop.MSBuild.Evaluation
{
	/// <summary>
	/// Provides MSBuild properties for the current project, including those form the environment and runtime
	/// </summary>
	class MSBuildProjectEvaluationContext : IMSBuildEvaluationContext
	{
		readonly Dictionary<string, EvaluatedValue> values = new (StringComparer.OrdinalIgnoreCase);
		readonly Dictionary<string, OneOrMany<EvaluatedValue>> projectSearchPathValues = new (StringComparer.OrdinalIgnoreCase);

		readonly IMSBuildEnvironment env;

		public ILogger Logger { get; }

		public MSBuildProjectEvaluationContext (IMSBuildEnvironment env, string? projectFilePath, ILogger logger)
		{
			this.env = env;

			Logger = logger;

			if (env is not NullMSBuildEnvironment) {
				PopulateRuntimeProperties (values, env);
			}

			if (projectFilePath is not null) {
				PopulateProjectProperties (values, projectFilePath);
			}
		}

		static void PopulateRuntimeProperties (Dictionary<string, EvaluatedValue> values, IMSBuildEnvironment env)
		{
			foreach (var toolsetProperty in env.ToolsetProperties) {
				values[toolsetProperty.Key] = EvaluatedValue.FromNativePath (toolsetProperty.Value);
			}

			// do our best to populate these values even if the toolset didn't explicitly have them

			// TODO there's a whole bunch more properties we should set here
			// see https://github.com/dotnet/msbuild/blob/d074c1250646c338f7eacb1ff8d9cbe5cf8ef3c6/src/Build/Evaluation/Evaluator.cs#L1124

			if (!values.ContainsKey (ReservedPropertyNames.binPath)) {
				values[ReservedPropertyNames.binPath] = EvaluatedValue.FromNativePath (env.ToolsPath);
			}
			if (!values.ContainsKey (ReservedPropertyNames.toolsPath)) {
				values[ReservedPropertyNames.toolsPath] = values[ReservedPropertyNames.binPath];
			}
			if (!values.ContainsKey (ReservedPropertyNames.toolsVersion)) {
				values[ReservedPropertyNames.toolsVersion] = new (env.ToolsVersion);
			}
			if (!values.ContainsKey (WellKnownProperties.VisualStudioVersion)) {
				values[WellKnownProperties.VisualStudioVersion] = new ("17.0");
			}

			if (RuntimeInformation.IsOSPlatform (OSPlatform.Windows)) {
				if (!values.ContainsKey (WellKnownProperties.MSBuildProgramFiles32)) {
					values[WellKnownProperties.MSBuildProgramFiles32] = EvaluatedValue.FromNativePath (Environment.GetFolderPath (Environment.SpecialFolder.ProgramFilesX86));
				}
				if (!values.ContainsKey (WellKnownProperties.MSBuildProgramFiles64)) {
					values[WellKnownProperties.MSBuildProgramFiles64] = EvaluatedValue.FromNativePath (Environment.GetFolderPath (Environment.SpecialFolder.ProgramFiles));
				}
			}
		}

		// see https://github.com/dotnet/msbuild/blob/d074c1250646c338f7eacb1ff8d9cbe5cf8ef3c6/src/Build/Evaluation/Evaluator.cs#L1171
		static void PopulateProjectProperties (Dictionary<string, EvaluatedValue> values, string projectPath)
		{
			// the path arguments should already be absolute but may not be for tests etc
			projectPath = Path.GetFullPath (projectPath);

			string? projectDirectory = Path.GetDirectoryName (projectPath);

			// TODO: should we be setting these at all? behavior for unsaved files without a path is very poorly defined
			if (projectDirectory is null) {
				values[ReservedPropertyNames.projectFile] = new ();
				values[ReservedPropertyNames.projectName] = new ();
				values[ReservedPropertyNames.projectFullPath] = new();
				values[ReservedPropertyNames.projectExtension] = new();
				values[ReservedPropertyNames.projectDirectory] = new();
				values[ReservedPropertyNames.projectDirectoryNoRoot] = new ();
				values[WellKnownProperties.MSBuildProjectExtensionsPath] = new ("obj");
				values[ReservedPropertyNames.startupDirectory] = new ();
				return;
			}

			values[ReservedPropertyNames.projectFile] = EvaluatedValue.FromUnescaped (Path.GetFileName (projectPath));
			values[ReservedPropertyNames.projectName] = EvaluatedValue.FromUnescaped (Path.GetFileNameWithoutExtension (projectPath));
			values[ReservedPropertyNames.projectFullPath] = EvaluatedValue.FromNativePath (projectPath);
			values[ReservedPropertyNames.projectExtension] = EvaluatedValue.FromUnescaped (Path.GetExtension (projectPath));
			values[ReservedPropertyNames.projectDirectory] = EvaluatedValue.FromNativePath (Path.GetDirectoryName (projectPath));

			int rootLength = Path.GetPathRoot (projectDirectory)?.Length ?? 0;
			values[ReservedPropertyNames.projectDirectoryNoRoot] = EvaluatedValue.FromNativePath (FileUtilities.EnsureNoLeadingOrTrailingSlash (projectDirectory, rootLength));

			//HACK: we don't get a usable value for this without real evaluation so hardcode 'obj'
			values[WellKnownProperties.MSBuildProjectExtensionsPath] = EvaluatedValue.FromNativePath (Path.Combine (projectDirectory, "obj") + Path.DirectorySeparatorChar);

			// this isn't technically correct - it's supposed to be the initial working directory of the msbuild exe
			// but the value that makes the most sense in the editor is the project directory
			values[ReservedPropertyNames.startupDirectory] = EvaluatedValue.FromNativePath (projectDirectory);
		}

		public bool TryGetProperty (string name, [NotNullWhen (true)] out EvaluatedValue? value)
		{
			if (values.TryGetValue (name, out var existingVal)) {
				value = existingVal;
				return true;
			}

			if (env.EnvironmentVariables.TryGetValue (name, out var envValue)) {
				var escapedValue = EvaluatedValue.FromNativePath (envValue);
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