// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using MonoDevelop.MSBuild.Util;

namespace MonoDevelop.MSBuild.Evaluation
{
	/// <summary>
	/// Provides MSBuild properties specific to the current file
	/// </summary>
	class MSBuildFileEvaluationContext : IMSBuildEvaluationContext
	{
		readonly Dictionary<string, EvaluatedValue> values = new(StringComparer.OrdinalIgnoreCase);
		readonly IMSBuildEvaluationContext runtimeContext;

		public MSBuildFileEvaluationContext (
			IMSBuildEvaluationContext runtimeContext,
			string projectPath,
			string thisFilePath)
		{
			this.runtimeContext = runtimeContext ?? throw new ArgumentNullException (nameof (runtimeContext));

			// this file path properties
			if (thisFilePath != null) {
				// the path arguments should already be absolute but may not be for tests etc
				var absFilePath = Path.GetFullPath (thisFilePath);
				values[ReservedProperties.ThisFile] = EvaluatedValue.FromUnescaped (Path.GetFileName (thisFilePath));
				values[ReservedProperties.ThisFileDirectory] = EvaluatedValue.FromNativePath (Path.GetDirectoryName (absFilePath) + "\\");
				//"MSBuildThisFileDirectoryNoRoot" is this actually used for anything?
				values[ReservedProperties.ThisFileExtension] = EvaluatedValue.FromUnescaped (Path.GetExtension (thisFilePath));
				values[ReservedProperties.ThisFileFullPath] = EvaluatedValue.FromNativePath (absFilePath);
				values[ReservedProperties.ThisFileName] = EvaluatedValue.FromUnescaped (Path.GetFileNameWithoutExtension (thisFilePath));
			}

			if (projectPath == null) {
				return;
			}

			// the path arguments should already be absolute but may not be for tests etc
			var absProjectPath = Path.GetFullPath (projectPath);
			var projectDir = Path.GetDirectoryName (absProjectPath);

			// project path properties
			values[ReservedProperties.ProjectDirectory] = EvaluatedValue.FromNativePath (projectDir);
			// "MSBuildProjectDirectoryNoRoot" is this actually used for anything?
			values[ReservedProperties.ProjectExtension] = EvaluatedValue.FromUnescaped (Path.GetExtension (projectPath));
			values[ReservedProperties.ProjectFile] = EvaluatedValue.FromUnescaped (Path.GetFileName (projectPath));
			values[ReservedProperties.ProjectFullPath] = EvaluatedValue.FromNativePath (absProjectPath);
			values[ReservedProperties.ProjectName] = EvaluatedValue.FromUnescaped (Path.GetFileNameWithoutExtension (projectPath));

			//don't have a better value, this is as good as anything
			values[ReservedProperties.StartupDirectory] = EvaluatedValue.FromNativePath (projectDir);

			//HACK: we don't get a usable value for this without real evaluation so hardcode 'obj'
			var projectExtensionsPath = Path.Combine (projectDir, "obj");
			values[ReservedProperties.ProjectExtensionsPath] = EvaluatedValue.FromNativePath (projectExtensionsPath + "\\");
		}

		public bool TryGetProperty (string name, [NotNullWhen (true)] out EvaluatedValue? value)
		{
			if (runtimeContext.TryGetProperty (name, out value)) {
				return true;
			}

			if (values.TryGetValue (name, out var v)) {
				value = v;
				return true;
			}

			return false;
		}

		public bool TryGetMultivaluedProperty (string name, [NotNullWhen (true)] out OneOrMany<EvaluatedValue>? value, bool isProjectImportStart = false)
		{
			if (runtimeContext.TryGetMultivaluedProperty (name, out value, isProjectImportStart)) {
				return true;
			}

			if (values.TryGetValue (name, out var v)) {
				value = v;
				return true;
			}

			return false;
		}
	}
}