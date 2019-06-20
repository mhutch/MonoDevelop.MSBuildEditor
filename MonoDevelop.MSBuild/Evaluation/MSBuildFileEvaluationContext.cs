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
			this.runtimeContext = runtimeContext;

			// project path properties
			string escapedProjectDir = MSBuildEscaping.ToMSBuildPath (Path.GetDirectoryName(projectPath));
			values["MSBuildProjectDirectory"] = escapedProjectDir;
			// "MSBuildProjectDirectoryNoRoot" is this actually used for anything?
			values["MSBuildProjectExtension"] = MSBuildEscaping.EscapeString (Path.GetExtension (projectPath));
			values["MSBuildProjectFile"] = MSBuildEscaping.EscapeString (Path.GetFileName (projectPath));
			values["MSBuildProjectFullPath"] = MSBuildEscaping.ToMSBuildPath (Path.GetFullPath(projectPath));
			values["MSBuildProjectName"] = MSBuildEscaping.EscapeString (Path.GetFileNameWithoutExtension (projectPath));

			//don't have a better value, this is as good as anything
			values["MSBuildStartupDirectory"] = escapedProjectDir;

			// this file path properties
			values["MSBuildThisFile"] = MSBuildEscaping.EscapeString (Path.GetFileName (thisFilePath));
			values["MSBuildThisFileDirectory"] = MSBuildEscaping.ToMSBuildPath (Path.GetDirectoryName(thisFilePath)) + "\\";
			//"MSBuildThisFileDirectoryNoRoot" is this actually used for anything?
			values["MSBuildThisFileExtension"] = MSBuildEscaping.EscapeString (Path.GetExtension (thisFilePath));
			values["MSBuildThisFileFullPath"] = MSBuildEscaping.ToMSBuildPath (Path.GetFullPath(thisFilePath));
			values["MSBuildThisFileName"] = MSBuildEscaping.EscapeString (Path.GetFileNameWithoutExtension (thisFilePath));

			//HACK: we don't get a usable value for this without real evaluation so hardcode 'obj'
			var projectExtensionsPath = Path.GetFullPath (Path.Combine (Path.GetDirectoryName (projectPath), "obj"));
			values["MSBuildProjectExtensionsPath"] = MSBuildEscaping.ToMSBuildPath (projectExtensionsPath) + "\\";
		}

		public bool TryGetProperty (string name, out MSBuildPropertyValue value)
			=> values.TryGetValue (name, out value)
			|| runtimeContext.TryGetProperty (name, out value);
	}
}