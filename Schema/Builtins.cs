// Copyright (c) 2015 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace MonoDevelop.MSBuildEditor.Schema
{

	static class Builtins
	{
		public static readonly Dictionary<string, MetadataInfo> Metadata = new Dictionary<string, MetadataInfo> {
			{
				"FullPath",
				new MetadataInfo (
					"FullPath",
					"The full path of the item",
					true
				)
			},
			{
				"RootDir",
				new MetadataInfo (
					"RootDir",
					"The root directory of the item",
					true
				)
			},
			{
				"Filename",
				new MetadataInfo (
					"Filename",
					"The filename of the item",
					true
				)
			},
			{
				"Extension",
				new MetadataInfo (
					"Extension",
					"The file extension of the item",
					true
				)
			},
			{
				"RelativeDir",
				new MetadataInfo (
					"RelativeDir",
					"The path specified in the Include attribute",
					true
				)
			},
			{
				"Directory",
				new MetadataInfo (
					"Directory",
					"The directory of the item, excluding the root directory",
					true
				)
			},
			{
				"RecursiveDir",
				new MetadataInfo (
					"RecursiveDir",
					"If the item contained a ** wildstar, the value to which it was expanded",
					true
				)
			},
			{
				"Identity",
				new MetadataInfo (
					"Identity",
					"The value specified in the Include attribute",
					true
				)
			},
			{
				"ModifiedTime",
				new MetadataInfo (
					"ModifiedTime",
					"The time the the item was last modified",
					true
				)
			},
			{
				"CreatedTime",
				new MetadataInfo (
					"CreatedTime",
					"The time the the item was created",
					true
				)
			},
			{
				"AccessedTime",
				new MetadataInfo (
					"AccessedTime",
					"The time the the item was last accessed",
					true
				)
			}
		};

		public static readonly Dictionary<string,PropertyInfo> Properties = new Dictionary<string, PropertyInfo> {
			{
				"MSBuildBinPath",
				new PropertyInfo (
					"MSBuildBinPath",
					"Absolute path of the bin directory where MSBuild is located. Does not include final backslash.",
					true, true
				)
			},
			{
				"MSBuildExtensionsPath",
				new PropertyInfo (
					"MSBuildExtensionsPath",
					"Absolute path of the MSBuild extensions directory for the current architecture. Does not include final backslash.",
					true, true
				)
			},
			{
				"MSBuildExtensionsPath32",
				new PropertyInfo (
					"MSBuildExtensionsPath32",
					"Absolute path of the 32-bit MSBuild extensions directory. Does not include final backslash.",
					true, true
				)
			},
			{
				"MSBuildExtensionsPath64",
				new PropertyInfo (
					"MSBuildExtensionsPath64",
					"Absolute path of the 64-bit MSBuild extensions directory. Does not include final backslash.",
					true, true
				)
			},
			{
				"MSBuildLastTaskResult",
				new PropertyInfo (
					"MSBuildLastTaskResult",
					"True if the last task completed without errors.",
					true, true
				)
			},
			{
				"MSBuildNodeCount",
				new PropertyInfo (
					"MSBuildNodeCount",
					"The number of concurrent build nodes.",
					true, true
				)
			},
			{
				"MSBuildProgramFiles32",
				new PropertyInfo (
					"MSBuildProgramFiles32",
					"Absolute path of the 32-bit Program Files folder. Does not include final backslash.",
					true, true
				)
			},
			{
				"MSBuildProjectDefaultTargets",
				new PropertyInfo (
					"MSBuildProjectDefaultTargets",
					"The value of the DefaultTargets attribute in the Project element.",
					true, true
				)
			},
			{
				"MSBuildProjectDirectory",
				new PropertyInfo (
					"MSBuildProjectDirectory",
					"Directory where the project file is located. Does not include final backslash.",
					true, true
				)
			},
			{
				"MSBuildProjectDirectoryNoRoot",
				new PropertyInfo (
					"MSBuildProjectDirectoryNoRoot",
					"Directory where the project file is located, excluding drive root. Does not include final backslash.",
					true, true
				)
			},
			{
				"MSBuildProjectFile",
				new PropertyInfo (
					"MSBuildProjectFile",
					"Name of the project file, including extension.",
					true, true
				)
			},
			{
				"MSBuildProjectFileFullPath",
				new PropertyInfo (
					"MSBuildProjectFileFullPath",
					"Full path of the project file.",
					true, true
				)
			},
			{
				"MSBuildProjectName",
				new PropertyInfo (
					"MSBuildProjectName",
					"Name of the project file, excluding extension.",
					true, true
				)
			},
			{
				"MSBuildStartupDirectory",
				new PropertyInfo (
					"MSBuildStartupDirectory",
					"Absolute path of the directory where MSBuild is invoked. Does not include final backslash.",
					true, true
				)
			},
			{
				"MSBuildThisFile",
				new PropertyInfo (
					"MSBuildThisFile",
					"Name of the current MSBuild file, including extension.",
					true, true
				)
			},
			{
				"MSBuildThisFileDirectory",
				new PropertyInfo (
					"MSBuildThisFileDirectory",
					"Directory where the current MSBuild file is located. Includes final backslash.",
					true, true
				)
			},
			{
				"MSBuildThisFileDirectoryNoRoot",
				new PropertyInfo (
					"MSBuildThisFileDirectoryNoRoot",
					"Directory where the current MSBuild file is located, excluding drive root. Includes final backslash.",
					true, true
				)
			},
			{
				"MSBuildThisFileExtension",
				new PropertyInfo (
					"MSBuildThisFileExtension",
					"Extension of the current MSBuild file.",
					true, true
				)
			},
			{
				"MSBuildThisFileFullPath",
				new PropertyInfo (
					"MSBuildThisFileFullPath",
					"Absolute path of the current MSBuild file is located.",
					true, true
				)
			},
			{
				"MSBuildThisFileName",
				new PropertyInfo (
					"MSBuildThisFileName",
					"Name of the current MSBuild file, excluding extension.",
					true, true
				)
			},
			{
				"MSBuildToolsPath",
				new PropertyInfo (
					"MSBuildToolsPath",
					"Path to the current toolset, specfied by the MSBuildToolsVersion. Does not include final backslash.",
					true, true
				)
			},
			{
				"MSBuildToolsVersion",
				new PropertyInfo (
					"MSBuildToolsVersion",
					"Version of the current toolset.",
					true, true
				)
			}
		};
	}
}