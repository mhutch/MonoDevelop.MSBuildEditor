// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using MonoDevelop.MSBuild.Util;

namespace MonoDevelop.MSBuild.Evaluation
{
	/// <summary>
	/// Provides MSBuild properties specific to the current file
	/// </summary>
	class MSBuildFileEvaluationContext : IMSBuildEvaluationContext
	{
		readonly Dictionary<string, MSBuildPropertyValue> values
			= new Dictionary<string, MSBuildPropertyValue> (StringComparer.OrdinalIgnoreCase);
		readonly IMSBuildEvaluationContext runtimeContext;

		public MSBuildFileEvaluationContext (
			IMSBuildEvaluationContext runtimeContext,
			string projectPath,
			string thisFilePath)
		{
			this.runtimeContext = runtimeContext ?? throw new ArgumentNullException (nameof (runtimeContext));

			// this file path properties
			if (thisFilePath != null) {
				values[ReservedProperties.ThisFile] = MSBuildEscaping.EscapeString (Path.GetFileName (thisFilePath));
				values[ReservedProperties.ThisFileDirectory] = MSBuildEscaping.ToMSBuildPath (Path.GetDirectoryName (thisFilePath)) + "\\";
				//"MSBuildThisFileDirectoryNoRoot" is this actually used for anything?
				values[ReservedProperties.ThisFileExtension] = MSBuildEscaping.EscapeString (Path.GetExtension (thisFilePath));
				values[ReservedProperties.ThisFileFullPath] = MSBuildEscaping.ToMSBuildPath (Path.GetFullPath (thisFilePath));
				values[ReservedProperties.ThisFileName] = MSBuildEscaping.EscapeString (Path.GetFileNameWithoutExtension (thisFilePath));
			}

			if (projectPath == null) {
				return;
			}

			// project path properties
			string escapedProjectDir = MSBuildEscaping.ToMSBuildPath (Path.GetDirectoryName(projectPath));
			values[ReservedProperties.ProjectDirectory] = escapedProjectDir;
			// "MSBuildProjectDirectoryNoRoot" is this actually used for anything?
			values[ReservedProperties.ProjectExtension] = MSBuildEscaping.EscapeString (Path.GetExtension (projectPath));
			values[ReservedProperties.ProjectFile] = MSBuildEscaping.EscapeString (Path.GetFileName (projectPath));
			values[ReservedProperties.ProjectFullPath] = MSBuildEscaping.ToMSBuildPath (Path.GetFullPath(projectPath));
			values[ReservedProperties.ProjectName] = MSBuildEscaping.EscapeString (Path.GetFileNameWithoutExtension (projectPath));

			//don't have a better value, this is as good as anything
			values[ReservedProperties.StartupDirectory] = escapedProjectDir;

			//HACK: we don't get a usable value for this without real evaluation so hardcode 'obj'
			var projectExtensionsPath = Path.GetFullPath (Path.Combine (Path.GetDirectoryName (projectPath), "obj"));
			values[ReservedProperties.ProjectExtensionsPath] = MSBuildEscaping.ToMSBuildPath (projectExtensionsPath) + "\\";
		}

		public bool TryGetProperty (string name, out MSBuildPropertyValue? value)
		{
			if (values.TryGetValue (name, out var v)) {
				value = v;
				return true;
			}

			return runtimeContext.TryGetProperty (name, out value);
		}
	}
}