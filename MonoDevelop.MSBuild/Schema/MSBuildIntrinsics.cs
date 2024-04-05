// Copyright (c) 2015 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using MonoDevelop.MSBuild.Language.Typesystem;

using ReservedPropertyNames = Microsoft.Build.Internal.ReservedPropertyNames;

namespace MonoDevelop.MSBuild.Schema
{
	static class MSBuildIntrinsics
	{
		public static Dictionary<string, MetadataInfo> Metadata { get; } = new Dictionary<string, MetadataInfo> (StringComparer.OrdinalIgnoreCase);
		public static Dictionary<string, PropertyInfo> Properties { get; } = new Dictionary<string, PropertyInfo> (StringComparer.OrdinalIgnoreCase);
		public static Dictionary<string, TaskInfo> Tasks { get; } = new Dictionary<string, TaskInfo> (StringComparer.OrdinalIgnoreCase);

		static void AddMetadata (string name, string description, MSBuildValueKind kind = MSBuildValueKind.Unknown, bool notReserved = false)
		{
			Metadata.Add (name, new MetadataInfo (name, description, !notReserved, false, kind));
		}

		static void AddReservedProperty (string name, string description, MSBuildValueKind kind, string helpUrl = null) => Properties.Add (name, new PropertyInfo (name, description, true, true, kind, helpUrl: helpUrl));
		static void AddReadOnlyProperty (string name, string description, MSBuildValueKind kind, string helpUrl = null) => Properties.Add (name, new PropertyInfo (name, description, false, true, kind, helpUrl: helpUrl));
		static void AddSettableProperty (string name, string description, MSBuildValueKind kind = MSBuildValueKind.Unknown, string helpUrl = null) => Properties.Add (name, new PropertyInfo (name, description, false, false, kind, helpUrl: helpUrl));

		static void AddTask (TaskInfo task) => Tasks.Add (task.Name, task);

		static MSBuildIntrinsics ()
		{
			AddMetadata ("FullPath", HelpDescriptions.WellKnownMetadata_FullPath, MSBuildValueKind.File);
			AddMetadata ("RootDir", HelpDescriptions.WellKnownMetadata_RootDir, MSBuildValueKind.FolderWithSlash);
			AddMetadata ("Filename", HelpDescriptions.WellKnownMetadata_Filename, MSBuildValueKind.Filename);
			AddMetadata ("Extension", HelpDescriptions.WellKnownMetadata_Extension, MSBuildValueKind.Extension);
			AddMetadata ("RelativeDir", HelpDescriptions.WellKnownMetadata_RelativeDir, MSBuildValueKind.FolderWithSlash);
			AddMetadata ("Directory", HelpDescriptions.WellKnownMetadata_Directory, MSBuildValueKind.FolderWithSlash);
			AddMetadata ("RecursiveDir", HelpDescriptions.WellKnownMetadata_RecursiveDir, MSBuildValueKind.FolderWithSlash);
			AddMetadata ("Identity", HelpDescriptions.WellKnownMetadata_Identity, MSBuildValueKind.MatchItem);
			AddMetadata ("ModifiedTime", HelpDescriptions.WellKnownMetadata_ModifiedTime, MSBuildValueKind.DateTime);
			AddMetadata ("CreatedTime", HelpDescriptions.WellKnownMetadata_CreatedTime, MSBuildValueKind.DateTime);
			AddMetadata ("AccessedTime", HelpDescriptions.WellKnownMetadata_AccessedTime, MSBuildValueKind.DateTime);
			AddMetadata ("DefiningProjectFullPath", HelpDescriptions.WellKnownMetadata_DefiningProjectFullPath, MSBuildValueKind.File);
			AddMetadata ("DefiningProjectDirectory", HelpDescriptions.WellKnownMetadata_DefiningProjectDirectory, MSBuildValueKind.Folder);
			AddMetadata ("DefiningProjectName", HelpDescriptions.WellKnownMetadata_DefiningProjectName, MSBuildValueKind.Filename);
			AddMetadata ("DefiningProjectExtension", HelpDescriptions.WellKnownMetadata_DefiningProjectExtension, MSBuildValueKind.Extension);

			AddReservedProperty (ReservedPropertyNames.binPath, HelpDescriptions.ReservedProperty_BinPath, MSBuildValueKind.Folder);
			AddReservedProperty (ReservedPropertyNames.toolsPath, HelpDescriptions.ReservedProperty_ToolsPath, MSBuildValueKind.Folder);
			AddReservedProperty (ReservedPropertyNames.toolsVersion, HelpDescriptions.ReservedProperty_ToolsVersion, MSBuildValueKind.ToolsVersion);
			AddReservedProperty (ReservedPropertyNames.assemblyVersion, HelpDescriptions.ReservedProperty_AssemblyVersion, MSBuildValueKind.Version);
			AddReservedProperty (ReservedPropertyNames.startupDirectory, HelpDescriptions.ReservedProperty_StartupDirectory, MSBuildValueKind.Folder);
			AddReservedProperty (ReservedPropertyNames.buildNodeCount, HelpDescriptions.ReservedProperty_BuildNodeCount, MSBuildValueKind.Int);
			AddReservedProperty (ReservedPropertyNames.lastTaskResult, HelpDescriptions.ReservedProperty_LastTaskResult, MSBuildValueKind.Bool);
			AddReservedProperty (ReservedPropertyNames.osName, HelpDescriptions.ReservedProperty_OSName, MSBuildValueKind.HostOS);
			AddReservedProperty (ReservedPropertyNames.msbuildRuntimeType, HelpDescriptions.ReservedProperty_MSBuildRuntimeType, MSBuildValueKind.HostRuntime);
			AddReservedProperty (ReservedPropertyNames.overrideTasksPath, HelpDescriptions.ReservedProperty_OverrideTasksPath, MSBuildValueKind.Folder);
			AddReservedProperty (ReservedPropertyNames.defaultOverrideToolsVersion, HelpDescriptions.ReservedProperty_DefaultOverrideToolsVersion, MSBuildValueKind.ToolsVersion);
			AddReservedProperty (ReservedPropertyNames.frameworkToolsRoot, HelpDescriptions.ReservedProperty_FrameworkToolsRoot, MSBuildValueKind.FolderWithSlash);
			AddReservedProperty (ReservedPropertyNames.userExtensionsPath, HelpDescriptions.ReservedProperty_UserExtensionsPath, MSBuildValueKind.Folder);

			AddReservedProperty (ReservedPropertyNames.projectDefaultTargets, HelpDescriptions.ReservedProperty_ProjectDefaultTargets, MSBuildValueKind.TargetName.AsList ());
			AddReservedProperty (ReservedPropertyNames.projectDirectory, HelpDescriptions.ReservedProperty_ProjectDirectory, MSBuildValueKind.Folder);
			AddReservedProperty (ReservedPropertyNames.projectDirectoryNoRoot, HelpDescriptions.ReservedProperty_ProjectDirectoryNoRoot, MSBuildValueKind.Folder);
			AddReservedProperty (ReservedPropertyNames.projectExtension, HelpDescriptions.ReservedProperty_ProjectExtension, MSBuildValueKind.Extension);
			AddReservedProperty (ReservedPropertyNames.projectFile, HelpDescriptions.ReservedProperty_ProjectFile, MSBuildValueKind.File);
			AddReservedProperty (ReservedPropertyNames.projectFullPath, HelpDescriptions.ReservedProperty_ProjectFullPath, MSBuildValueKind.File);
			AddReservedProperty (ReservedPropertyNames.projectName, HelpDescriptions.ReservedProperty_ProjectName, MSBuildValueKind.Filename);

			AddReservedProperty (ReservedPropertyNames.thisFile, HelpDescriptions.ReservedProperty_ThisFile, MSBuildValueKind.Filename);
			AddReservedProperty (ReservedPropertyNames.thisFileDirectory, HelpDescriptions.ReservedProperty_ThisFileDirectory, MSBuildValueKind.FolderWithSlash);
			AddReservedProperty (ReservedPropertyNames.thisFileDirectoryNoRoot, HelpDescriptions.ReservedProperty_ThisFileDirectoryNoRoot, MSBuildValueKind.FolderWithSlash);
			AddReservedProperty (ReservedPropertyNames.thisFileExtension, HelpDescriptions.ReservedProperty_ThisFileExtension, MSBuildValueKind.Extension);
			AddReservedProperty (ReservedPropertyNames.thisFileFullPath, HelpDescriptions.ReservedProperty_ThisFileFullPath, MSBuildValueKind.File);
			AddReservedProperty (ReservedPropertyNames.thisFileName, HelpDescriptions.ReservedProperty_ThisFileName, MSBuildValueKind.File);


			AddReadOnlyProperty (WellKnownProperties.MSBuildExtensionsPath, HelpDescriptions.WellKnownProperty_MSBuildExtensionsPath, MSBuildValueKind.Folder);
			AddReadOnlyProperty (WellKnownProperties.MSBuildExtensionsPath32, HelpDescriptions.WellKnownProperty_MSBuildExtensionsPath32, MSBuildValueKind.Folder);
			AddReadOnlyProperty (WellKnownProperties.MSBuildExtensionsPath64, HelpDescriptions.WellKnownProperty_MSBuildExtensionsPath64, MSBuildValueKind.Folder);
			AddReadOnlyProperty (WellKnownProperties.MSBuildProgramFiles32, HelpDescriptions.WellKnownProperty_MSBuildProgramFiles32, MSBuildValueKind.Folder);

			AddSettableProperty (WellKnownProperties.MSBuildTreatWarningsAsErrors, HelpDescriptions.WellKnownProperty_MSBuildTreatWarningsAsErrors, MSBuildValueKind.Bool);
			AddSettableProperty (WellKnownProperties.MSBuildWarningsAsErrors, HelpDescriptions.WellKnownProperty_MSBuildWarningsAsErrors, MSBuildValueKind.WarningCode.AsList ());
			AddSettableProperty (WellKnownProperties.MSBuildWarningsNotAsErrors, HelpDescriptions.WellKnownProperty_MSBuildWarningsNotAsErrors, MSBuildValueKind.WarningCode.AsList ());
			AddSettableProperty (WellKnownProperties.MSBuildWarningsAsMessages, HelpDescriptions.WellKnownProperty_MSBuildWarningsAsMessages, MSBuildValueKind.WarningCode.AsList ());
			AddSettableProperty (WellKnownProperties.MSBuildAllProjects, HelpDescriptions.WellKnownProperty_MSBuildAllProjects, MSBuildValueKind.ProjectFile);

			AddTask (new TaskInfo (
				"CallTarget",
				HelpDescriptions.Task_CallTarget,
				new TaskParameterInfo ("RunEachTargetSeparately", HelpDescriptions.Task_CallTarget_RunEachTargetSeparately, false, false, MSBuildValueKind.Bool),
				new TaskParameterInfo ("TargetOutputs", HelpDescriptions.Task_CallTarget_TargetOutputs, false, true, MSBuildValueKind.UnknownItem.AsList ()),
				new TaskParameterInfo ("Targets", HelpDescriptions.Task_CallTarget_Targets, true, false, MSBuildValueKind.TargetName.AsList ()),
				new TaskParameterInfo ("UseResultsCache", HelpDescriptions.Task_CallTarget_UseResultCache, true, false, MSBuildValueKind.Bool)
			));

			AddTask (new TaskInfo (
				"MSBuild",
				HelpDescriptions.Task_MSBuild,
				new TaskParameterInfo ("BuildInParallel", HelpDescriptions.Task_MSBuild_BuildInParallel, false, false, MSBuildValueKind.Bool),
				new TaskParameterInfo ("Projects", HelpDescriptions.Task_MSBuild_Projects, true, false, MSBuildValueKind.ProjectFile.AsList ()),
				new TaskParameterInfo ("Properties", HelpDescriptions.Task_MSBuild_Properties, false, false, MSBuildValueKind.String.AsList ()),
				new TaskParameterInfo ("RebaseOutputs", HelpDescriptions.Task_MSBuild_RebaseOutputs, false, false, MSBuildValueKind.Bool),
				new TaskParameterInfo ("RemoveProperties", HelpDescriptions.Task_MSBuild_RemoveProperties, false, false, MSBuildValueKind.PropertyName.AsList ()),
				new TaskParameterInfo ("RunEachTargetSeparately", HelpDescriptions.Task_CallTarget_RunEachTargetSeparately, false, false, MSBuildValueKind.Bool),
				new TaskParameterInfo ("SkipNonexistentProjects", HelpDescriptions.Task_MSBuild_SkipNonexistentProjects, false, false, MSBuildValueKind.SkipNonexistentProjectsBehavior),
				new TaskParameterInfo ("StopOnFirstFailure", HelpDescriptions.Task_MSBuild_StopOnFirstFailure, false, false, MSBuildValueKind.Bool),
				new TaskParameterInfo ("TargetAndPropertyListSeparators", HelpDescriptions.Task_MSBuild_TargetAndPropertyListSeparators, false, false, MSBuildValueKind.String.AsList ()),
				new TaskParameterInfo ("TargetOutputs", HelpDescriptions.Task_MSBuild_TargetOutputs, false, true, MSBuildValueKind.UnknownItem.AsList ()),
				new TaskParameterInfo ("Targets", HelpDescriptions.Task_MSBuild_Targets, false, false, MSBuildValueKind.TargetName.AsList ()),
				new TaskParameterInfo ("ToolsVersion", HelpDescriptions.Task_MSBuild_ToolsVersion, false, false, MSBuildValueKind.ToolsVersion),
				new TaskParameterInfo ("UnloadProjectsOnCompletion", HelpDescriptions.Task_MSBuild_UnloadProjectsOnCompletion, false, false, MSBuildValueKind.Bool),
				new TaskParameterInfo ("UseResultsCache", HelpDescriptions.Task_MSBuild_UseResultsCache, true, false, MSBuildValueKind.Bool)
			));
		}

		public static Dictionary<string, FunctionInfo> ConditionFunctions { get; }
			= new Dictionary<string, FunctionInfo> (StringComparer.OrdinalIgnoreCase) {
				{
					"Exists",
					new FunctionInfo (
						"Exists",
						HelpDescriptions.ConditionFunction_Exists,
						MSBuildValueKind.Bool,
						[
							new FunctionParameterInfo (
								"path",
								HelpDescriptions.ConditionFunction_Exists_path,
								MSBuildValueKind.FileOrFolder
							)
						]
					)
				},
				{
					"HasTrailingSlash",
					new FunctionInfo (
						"HasTrailingSlash",
						HelpDescriptions.ConditionFunction_HasTrailingSlash,
						MSBuildValueKind.Bool,
						[
							new FunctionParameterInfo (
								"value",
								HelpDescriptions.ConditionFunction_HasTrailingSlash_value,
								MSBuildValueKind.String
							)
						]
					)
				}
			};
	}
}