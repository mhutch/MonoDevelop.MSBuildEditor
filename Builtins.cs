//
// Builtins.cs
//
// Author:
//       mhutch <m.j.hutchinson@gmail.com>
//
// Copyright (c) 2015 Xamarin Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System.Collections.Generic;

namespace MonoDevelop.MSBuildEditor
{

	static class Builtins
	{
		public static readonly Dictionary<string, MetadataInfo> Metadata = new Dictionary<string, MSBuildEditor.MetadataInfo> {
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
					"The toot directory of the item",
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
					reserved: true
				)
			},
			{
				"MSBuildExtensionsPath",
				new PropertyInfo (
					"MSBuildExtensionsPath",
					"Absolute path of the MSBuild extensions directory for the current architecture. Does not include final backslash.",
					wellKnown: true
				)
			},
			{
				"MSBuildExtensionsPath32",
				new PropertyInfo (
					"MSBuildExtensionsPath32",
					"Absolute path of the 32-bit MSBuild extensions directory. Does not include final backslash.",
					wellKnown: true
				)
			},
			{
				"MSBuildExtensionsPath64",
				new PropertyInfo (
					"MSBuildExtensionsPath64",
					"Absolute path of the 64-bit MSBuild extensions directory. Does not include final backslash.",
					wellKnown: true
				)
			},
			{
				"MSBuildLastTaskResult",
				new PropertyInfo (
					"MSBuildLastTaskResult",
					"True if the last task completed without errors.",
					reserved: true
				)
			},
			{
				"MSBuildNodeCount",
				new PropertyInfo (
					"MSBuildNodeCount",
					"The number of concurrent build nodes.",
					reserved: true
				)
			},
			{
				"MSBuildProgramFiles32",
				new PropertyInfo (
					"MSBuildProgramFiles32",
					"Absolute path of the 32-bit Program Files folder. Does not include final backslash.",
					reserved: true
				)
			},
			{
				"MSBuildProjectDefaultTargets",
				new PropertyInfo (
					"MSBuildProjectDefaultTargets",
					"The value of the DefaultTargets attribute in the Project element.",
					reserved: true
				)
			},
			{
				"MSBuildProjectDirectory",
				new PropertyInfo (
					"MSBuildProjectDirectory",
					"Directory where the project file is located. Does not include final backslash.",
					reserved: true
				)
			},
			{
				"MSBuildProjectDirectoryNoRoot",
				new PropertyInfo (
					"MSBuildProjectDirectoryNoRoot",
					"Directory where the project file is located, excluding drive root. Does not include final backslash.",
					reserved: true
				)
			},
			{
				"MSBuildProjectFile",
				new PropertyInfo (
					"MSBuildProjectFile",
					"Name of the project file, including extension.",
					reserved: true
				)
			},
			{
				"MSBuildProjectFileFullPath",
				new PropertyInfo (
					"MSBuildProjectFileFullPath",
					"Full path of the project file.",
					reserved: true
				)
			},
			{
				"MSBuildProjectName",
				new PropertyInfo (
					"MSBuildProjectName",
					"Name of the project file, excluding extension.",
					reserved: true
				)
			},
			{
				"MSBuildStartupDirectory",
				new PropertyInfo (
					"MSBuildStartupDirectory",
					"Absolute path of the directory where MSBuild is invoked. Does not include final backslash.",
					reserved: true
				)
			},
			{
				"MSBuildThisFile",
				new PropertyInfo (
					"MSBuildThisFile",
					"Name of the current MSBuild file, including extension.",
					reserved: true
				)
			},
			{
				"MSBuildThisFileDirectory",
				new PropertyInfo (
					"MSBuildThisFileDirectory",
					"Directory where the current MSBuild file is located. Includes final backslash.",
					reserved: true
				)
			},
			{
				"MSBuildThisFileDirectoryNoRoot",
				new PropertyInfo (
					"MSBuildThisFileDirectoryNoRoot",
					"Directory where the current MSBuild file is located, excluding drive root. Includes final backslash.",
					reserved: true
				)
			},
			{
				"MSBuildThisFileExtension",
				new PropertyInfo (
					"MSBuildThisFileExtension",
					"Extension of the current MSBuild file.",
					reserved: true
				)
			},
			{
				"MSBuildThisFileFullPath",
				new PropertyInfo (
					"MSBuildThisFileFullPath",
					"Absolute path of the current MSBuild file is located.",
					reserved: true
				)
			},
			{
				"MSBuildThisFileName",
				new PropertyInfo (
					"MSBuildThisFileName",
					"Name of the current MSBuild file, excluding extension.",
					reserved: true
				)
			},
			{
				"MSBuildToolsPath",
				new PropertyInfo (
					"MSBuildToolsPath",
					"Path to the current toolset, specfied by the MSBuildToolsVersion. Does not include final backslash.",
					reserved: true
				)
			},
			{
				"MSBuildToolsVersion",
				new PropertyInfo (
					"MSBuildToolsVersion",
					"Version of the current toolset.",
					reserved: true
				)
			}
		};
	}
}