// Copyright (c) 2015 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace MonoDevelop.MSBuild.Schema
{

	static class Builtins
	{
		public static Dictionary<string, MetadataInfo> Metadata { get; } = new Dictionary<string, MetadataInfo> ();
		public static Dictionary<string, PropertyInfo> Properties { get; } = new Dictionary<string, PropertyInfo> ();
		public static Dictionary<string, TaskInfo> Tasks { get; } = new Dictionary<string, TaskInfo> ();

		static void AddMetadata (string name, string description, MSBuildValueKind kind = MSBuildValueKind.Unknown, bool notReserved = false)
		{
			Metadata.Add (name, new MetadataInfo (name, description, !notReserved, false, kind));
		}

		static void AddProperty (string name, string description, MSBuildValueKind kind = MSBuildValueKind.Unknown, bool notReserved = false)
		{
			Properties.Add (name, new PropertyInfo (name, description, !notReserved, kind));
		}

		static void AddTask (TaskInfo task) => Tasks.Add (task.Name, task);

		static Builtins ()
		{
			AddMetadata ("FullPath", "The full path of the item", MSBuildValueKind.File);
			AddMetadata ("RootDir", "The root directory of the item", MSBuildValueKind.FolderWithSlash);
			AddMetadata ("Filename", "The filename of the item", MSBuildValueKind.Filename);
			AddMetadata ("Extension", "The file extension of the item", MSBuildValueKind.Extension);
			AddMetadata ("RelativeDir", "The path specified in the Include attribute", MSBuildValueKind.FolderWithSlash);
			AddMetadata ("Directory", "The directory of the item, excluding the root directory", MSBuildValueKind.FolderWithSlash);
			AddMetadata ("RecursiveDir", "If the item contained a ** wildstar, the value to which it was expanded", MSBuildValueKind.FolderWithSlash);
			AddMetadata ("Identity", "The value specified in the Include attribute", MSBuildValueKind.MatchItem);
			AddMetadata ("ModifiedTime", "The time the the item was last modified", MSBuildValueKind.DateTime);
			AddMetadata ("CreatedTime", "The time the the item was created", MSBuildValueKind.DateTime);
			AddMetadata ("AccessedTime", "The time the the item was last accessed", MSBuildValueKind.DateTime);
			AddMetadata ("DefiningProjectFullPath", "The full path of the project in which this item was defined", MSBuildValueKind.File);
			AddMetadata ("DefiningProjectDirectory", "The directory of the project in which this item was defined", MSBuildValueKind.Folder);
			AddMetadata ("DefiningProjectName", "The name of the project in which this item was defined", MSBuildValueKind.Filename);
			AddMetadata ("DefiningProjectExtension", "The extension of the project in which this item was defined", MSBuildValueKind.Extension);

			AddProperty ("MSBuildBinPath", "Absolute path of the bin directory where MSBuild is located. Does not include final backslash.", MSBuildValueKind.Folder);
			AddProperty ("MSBuildExtensionsPath", "Absolute path of the MSBuild extensions directory for the current architecture. Does not include final backslash.", MSBuildValueKind.Folder);
			AddProperty ("MSBuildExtensionsPath32", "Absolute path of the 32-bit MSBuild extensions directory. Does not include final backslash.", MSBuildValueKind.Folder);
			AddProperty ("MSBuildExtensionsPath64", "Absolute path of the 64-bit MSBuild extensions directory. Does not include final backslash.", MSBuildValueKind.Folder);
			AddProperty ("MSBuildLastTaskResult", "True if the last task completed without errors.", MSBuildValueKind.Bool);
			AddProperty ("MSBuildNodeCount", "The number of concurrent build nodes.", MSBuildValueKind.Int);
			AddProperty ("MSBuildProgramFiles32", "Absolute path of the 32-bit Program Files folder. Does not include final backslash.", MSBuildValueKind.Folder);
			AddProperty ("MSBuildProjectDefaultTargets", "The value of the DefaultTargets attribute in the Project element.", MSBuildValueKind.TargetName.List ());
			AddProperty ("MSBuildProjectDirectory", "Directory where the project file is located. Does not include final backslash.", MSBuildValueKind.Folder);
			AddProperty ("MSBuildProjectDirectoryNoRoot", "Directory where the project file is located, excluding drive root. Does not include final backslash.", MSBuildValueKind.Folder);
			AddProperty ("MSBuildProjectFile", "Name of the project file, including extension.", MSBuildValueKind.File);
			AddProperty ("MSBuildProjectExtension", "Extension of the project file.", MSBuildValueKind.Extension);
			AddProperty ("MSBuildProjectFullPath", "Full path of the project file.", MSBuildValueKind.File);
			AddProperty ("MSBuildProjectName", "Name of the project file, excluding extension.", MSBuildValueKind.Filename);
			AddProperty ("MSBuildStartupDirectory", "Absolute path of the directory where MSBuild is invoked. Does not include final backslash.", MSBuildValueKind.Folder);
			AddProperty ("MSBuildThisFile", "Name of the current MSBuild file, including extension.", MSBuildValueKind.Filename);
			AddProperty ("MSBuildThisFileDirectory", "Directory where the current MSBuild file is located.", MSBuildValueKind.FolderWithSlash);
			AddProperty ("MSBuildThisFileDirectoryNoRoot", "Directory where the current MSBuild file is located, excluding drive root.", MSBuildValueKind.FolderWithSlash);
			AddProperty ("MSBuildThisFileExtension", "Extension of the current MSBuild file.", MSBuildValueKind.Extension);
			AddProperty ("MSBuildThisFileFullPath", "Absolute path of the current MSBuild file.", MSBuildValueKind.File);
			AddProperty ("MSBuildThisFileName", "Name of the current MSBuild file, excluding extension.", MSBuildValueKind.File);
			AddProperty ("MSBuildToolsPath", "Path to the current toolset, specfied by the MSBuildToolsVersion. Does not include final backslash.", MSBuildValueKind.Folder);
			AddProperty ("MSBuildToolsVersion", "Version of the current toolset", MSBuildValueKind.ToolsVersion);
			AddProperty ("MSBuildUserExtensionsPath", "Directory from which user extensions are imported. Does not include final backslash.", MSBuildValueKind.Folder);
			AddProperty ("MSBuildRuntimeType", "The runtime on which MSBuild is running", MSBuildValueKind.HostRuntime);
			AddProperty ("MSBuildOverrideTasksPath", "Path to files that override built-in tasks", MSBuildValueKind.Folder);
			AddProperty ("DefaultOverrideToolsVersion", "Tools version to override the built-in tools version", MSBuildValueKind.ToolsVersion);
			AddProperty ("MSBuildAssemblyVersion", "The version of the MSBuild assemblies", MSBuildValueKind.Version);
			AddProperty ("OS", "The OS on which MSBuild is running", MSBuildValueKind.HostOS);
			AddProperty ("MSBuildFrameworkToolsRoot", "The root directory of the .NET framework tools", MSBuildValueKind.FolderWithSlash);

			AddProperty ("MSBuildTreatWarningsAsErrors", "Name of the property that indicates that all warnings should be treated as errors", MSBuildValueKind.PropertyName, true);
			AddProperty ("MSBuildWarningsAsErrors", "Name of the property that indicates a list of warnings to treat as errors", MSBuildValueKind.PropertyName, true);
			AddProperty ("MSBuildWarningsAsMessages", "Name of the property that indicates the list of warnings to treat as messages", MSBuildValueKind.PropertyName, true);

			AddTask (new TaskInfo (
				"CallTarget",
				"Invokes the specified targets in the current file",
				true,
				new TaskParameterInfo ("RunEachTargetSeparately", "Whether the MSBuild engine should be called once per target", false, false, MSBuildValueKind.Bool),
				new TaskParameterInfo ("TargetOutputs", "Items returned from all built targets", false, true, MSBuildValueKind.UnknownItem.List ()),
				new TaskParameterInfo ("Targets", "The targets to be invoked", true, false, MSBuildValueKind.TargetName.List ()),
				new TaskParameterInfo ("UseResultsCache", "Whether to use existing cached target outputs", true, false, MSBuildValueKind.Bool)
			));

			AddTask (new TaskInfo (
				"MSBuild",
				"Invokes the specified targets on the specified MSBuild projects",
				true,
				new TaskParameterInfo ("BuildInParallel", "Whether to build the projects in parallel", false, false, MSBuildValueKind.Bool),
				new TaskParameterInfo ("Projects", "The project files on which to invoke the targets", true, false, MSBuildValueKind.ProjectFile.List ()),
				new TaskParameterInfo ("Properties", "Semicolon-separated list of `PropertyName=Value` values to pass to the child projects", false, false, MSBuildValueKind.String.List ()),
				new TaskParameterInfo ("RebaseOutputs", "Rebases returned items with relative paths to be relative to the current project", false, false, MSBuildValueKind.Bool),
				new TaskParameterInfo ("RemoveProperties", "Semicolon-separated list of global properties to remove when invoking the child projects", false, false, MSBuildValueKind.PropertyName.List ()),
				new TaskParameterInfo ("RunEachTargetSeparately", "Whether the MSBuild engine should be called once per target", false, false, MSBuildValueKind.Bool),
				new TaskParameterInfo ("SkipNonexistentProjects", "Skip nonexistent projects instead of erroring out", false, false, MSBuildValueKind.SkipNonexistentProjectsBehavior),
				new TaskParameterInfo ("StopOnFirstFailure", "If target invocation fails on one project, do not continue with any other projects. Does not work with parallel builds.", false, false, MSBuildValueKind.Bool),
				new TaskParameterInfo ("TargetAndPropertyListSeparators", "Additional custom separators to use for splitting `Properties` and `Targets` parameters into lists.", false, false, MSBuildValueKind.String.List ()),
				new TaskParameterInfo ("TargetOutputs", "Items returned from all built targets", false, true, MSBuildValueKind.UnknownItem.List ()),
				new TaskParameterInfo ("Targets", "The targets to be invoked", false, false, MSBuildValueKind.TargetName.List ()),
				new TaskParameterInfo ("ToolsVersion", "Override the ToolsVersion used to build the projects", false, false, MSBuildValueKind.ToolsVersion),
				new TaskParameterInfo ("UnloadProjectsOnCompletion", "Unload the projects after invoking targets on them", false, false, MSBuildValueKind.Bool),
				new TaskParameterInfo ("UseResultsCache", "Whether to use existing cached target outputs", true, false, MSBuildValueKind.Bool)
			));
		}

		public static Dictionary<string, FunctionInfo> ConditionFunctions { get; }
			= new Dictionary<string, FunctionInfo> (StringComparer.OrdinalIgnoreCase) {
				{
					"Exists",
					new FunctionInfo (
						"Exists",
						"Checks whether the specified file or folder exists",
						MSBuildValueKind.Bool,
						new FunctionParameterInfo (
							"path",
							"File or folder path to check",
							MSBuildValueKind.FileOrFolder
						)
					)
				},
				{
					"HasTrailingSlash",
					new FunctionInfo (
						"HasTrailingSlash",
						"Checks whether a string has a trailing forward or backward slash character",
						MSBuildValueKind.Bool,
						new FunctionParameterInfo (
							"value",
							"The string to check",
							MSBuildValueKind.String
						)
					)
				}
			};
	}
}