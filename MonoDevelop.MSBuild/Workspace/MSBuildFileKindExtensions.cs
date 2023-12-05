// Copyright (c) 2014 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System;

/* Unmerged change from project 'MonoDevelop.MSBuild (net6.0)'
Before:
using System.Diagnostics;
After:
using System.Diagnostics;
using MonoDevelop;
using MonoDevelop.MSBuild;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Workspace;
*/
using System.Diagnostics;

namespace MonoDevelop.MSBuild.Workspace
{
	public static class MSBuildFileKindExtensions
	{
		public static MSBuildFileKind GetFileKind (string? filename)
		{
			if (filename is null) {
				return MSBuildFileKind.Unknown;
			}

			var ext = Path.GetExtension (filename);
			if (ext is null) {
				return MSBuildFileKind.Unknown;
			}

			var kind = GetProjectFileKind (ext);

			if (kind != MSBuildFileKind.UserProj) {
				return kind;
			}

			// If it has a *.user extension, determine the type of project for which it's a user settings files.
			// If this can't be done, then treat it as unknown.

			var userProjExt = Path.GetExtension (Path.GetFileNameWithoutExtension (filename));
			if (userProjExt is null) {
				return MSBuildFileKind.Unknown;
			}

			var userProjKind = GetProjectFileKind (userProjExt);
			if (userProjKind.IsProject ()) {
				return userProjKind | MSBuildFileKind.UserProj;
			}

			return MSBuildFileKind.Unknown;
		}

		static MSBuildFileKind GetProjectFileKind (string extension)
		{
			if (string.Equals (extension, MSBuildFileExtension.props, StringComparison.OrdinalIgnoreCase)) {
				return MSBuildFileKind.Props;
			}
			if (string.Equals (extension, MSBuildFileExtension.targets, StringComparison.OrdinalIgnoreCase)) {
				return MSBuildFileKind.Targets;
			}
			if (string.Equals (extension, MSBuildFileExtension.overridetasks, StringComparison.OrdinalIgnoreCase)) {
				return MSBuildFileKind.OverrideTasks;
			}
			if (string.Equals (extension, MSBuildFileExtension.tasks, StringComparison.OrdinalIgnoreCase)) {
				return MSBuildFileKind.Tasks;
			}
			if (string.Equals (extension, MSBuildFileExtension.csproj, StringComparison.OrdinalIgnoreCase)) {
				return MSBuildFileKind.CSProj;
			}
			if (string.Equals (extension, MSBuildFileExtension.vbproj, StringComparison.OrdinalIgnoreCase)) {
				return MSBuildFileKind.VBProj;
			}
			if (string.Equals (extension, MSBuildFileExtension.fsproj, StringComparison.OrdinalIgnoreCase)) {
				return MSBuildFileKind.FSProj;
			}
			if (string.Equals (extension, MSBuildFileExtension.xproj, StringComparison.OrdinalIgnoreCase)) {
				return MSBuildFileKind.XProj;
			}
			if (string.Equals (extension, MSBuildFileExtension.vcxproj, StringComparison.OrdinalIgnoreCase)) {
				return MSBuildFileKind.VcxProj;
			}
			if (string.Equals (extension, MSBuildFileExtension.sfxproj, StringComparison.OrdinalIgnoreCase)) {
				return MSBuildFileKind.SfxProj;
			}
			if (string.Equals (extension, MSBuildFileExtension.pubxml, StringComparison.OrdinalIgnoreCase)) {
				return MSBuildFileKind.PubXml;
			}
			if (extension.EndsWith ("proj")) {
				return MSBuildFileKind.Project;
			}
			return MSBuildFileKind.Unknown;
		}

		public static bool IsProject (this MSBuildFileKind kind) => kind.HasFlag (MSBuildFileKind.Project) && !kind.HasFlag (MSBuildFileKind.UserProj);
		public static bool IsUserProj (this MSBuildFileKind kind) => kind.HasFlag (MSBuildFileKind.UserProj);
		public static bool IsProjectOrUserProj (this MSBuildFileKind kind) => kind.HasFlag (MSBuildFileKind.Project);

		public static bool IsUserProj (this MSBuildFileKind kind, out MSBuildFileKind projectKind)
		{
			projectKind = kind ^ MSBuildFileKind.UserProj;

			if (kind.HasFlag (MSBuildFileKind.UserProj)) {
				Debug.Assert (projectKind.HasFlag (MSBuildFileKind.Project), "MSBuildFileKind.UserProj is a flag that must be applied to a project");
				return true;
			}

			return false;
		}

		public static DisplayText? GetDescription (MSBuildFileKind fileKind)
		{
			bool isUserProj = fileKind.IsUserProj (out fileKind);

			return fileKind switch {
				MSBuildFileKind.Tasks => "Task registration file in the MSBuild bin directory that registers default tasks",
				MSBuildFileKind.OverrideTasks => "Task override file in the MSBuild bin directory that overrides tasks defined in the .tasks file and in project and targets files",
				MSBuildFileKind.Props => "Props file that contains definitions to be imported at the beginning of the project file, such as properties and items that may be referenced in the project file at evaluation time",
				MSBuildFileKind.Targets => "Targets file that contains logic to be imported at the end of the project file, such as target definitions, or evaluation-time definitions that depend on values from the project file",
				MSBuildFileKind.PubXml => "Publish profile that contains a set of properties to be used when publishing a project.",
				MSBuildFileKind.CSProj => isUserProj ? "User settings for a C# project file" : "C# project file",
				MSBuildFileKind.VBProj => isUserProj ? "User settings for a Visual Basic project file" : "Visual Basic project file",
				MSBuildFileKind.FSProj => isUserProj ? "User settings for an F# project file" : "F# project file",
				MSBuildFileKind.XProj => isUserProj ? "User settings for a generic MonoDevelop project file" : "Generic MonoDevelop project file",
				MSBuildFileKind.VcxProj => isUserProj ? "User settings for a C++ project file" : "C++ project file",
				MSBuildFileKind.SfxProj => isUserProj ? "User settings for a Shared Framework project file" : "Shared Framework project file",
				MSBuildFileKind.Project => isUserProj ? "User settings for a project file" : "Project file",
				_ => null
			};
		}
	}
}