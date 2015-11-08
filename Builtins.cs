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

namespace MonoDevelop.MSBuildEditor
{

	static class Builtins
	{
		public static readonly MetadataInfo [] WellKnownMetadata = {
			new MetadataInfo (
				"FullPath",
				"The full path of the item",
				true
			),
			new MetadataInfo (
				"RootDir",
				"The toot directory of the item",
				true
			),
			new MetadataInfo (
				"Filename",
				"The filename of the item",
				true
			),
			new MetadataInfo (
				"Extension",
				"The file extension of the item",
				true
			),
			new MetadataInfo (
				"RelativeDir",
				"The path specified in the Include attribute",
				true
			),
			new MetadataInfo (
				"Directory",
				"The directory of the item, excluding the root directory",
				true
			),
			new MetadataInfo (
				"RecursiveDir",
				"If the item contained a ** wildstar, the value to which it was expanded",
				true
			),
			new MetadataInfo (
				"Identity",
				"The value specified in the Include attribute",
				true
			),
			new MetadataInfo (
				"ModifiedTime",
				"The time the the item was last modified",
				true
			),
			new MetadataInfo (
				"CreatedTime",
				"The time the the item was created",
				true
			),
			new MetadataInfo (
				"AccessedTime",
				"The time the the item was last accessed",
				true
			)
		};

		public static readonly PropertyInfo [] PropertyInfo = {
			new PropertyInfo (
				"MSBuildBinPath",
				"Absolute path of the bin directory where MSBuild is located. Does not include final backslash.",
				reserved: true
			),
			new PropertyInfo (
				"MSBuildExtensionsPath",
				"Absolute path of the MSBuild extensions directory for the current architecture. Does not include final backslash.",
				wellKnown: true
			),
			new PropertyInfo (
				"MSBuildExtensionsPath32",
				"Absolute path of the 32-bit MSBuild extensions directory. Does not include final backslash.",
				wellKnown: true
			),
			new PropertyInfo (
				"MSBuildExtensionsPath64",
				"Absolute path of the 64-bit MSBuild extensions directory. Does not include final backslash.",
				wellKnown: true
			),
			new PropertyInfo (
				"MSBuildLastTaskResult",
				"True if the last task completed without errors.",
				reserved: true
			),
			new PropertyInfo (
				"MSBuildNodeCount",
				"The number of concurrent build nodes.",
				reserved: true
			),
			new PropertyInfo (
				"MSBuildProgramFiles32",
				"Absolute path of the 32-bit Program Files folder. Does not include final backslash.",
				reserved: true
			),
			new PropertyInfo (
				"MSBuildProjectDefaultTargets",
				"The value of the DefaultTargets attribute in the Project element.",
				reserved: true
			),
			new PropertyInfo (
				"MSBuildProjectDirectory",
				"Directory where the project file is located. Does not include final backslash.",
				reserved: true
			),
			new PropertyInfo (
				"MSBuildProjectDirectoryNoRoot",
				"Directory where the project file is located, excluding drive root. Does not include final backslash.",
				reserved: true
			),
			new PropertyInfo (
				"MSBuildProjectFile",
				"Name of the project file, including extension.",
				reserved: true
			),
			new PropertyInfo (
				"MSBuildProjectFileFullPath",
				"Full path of the project file.",
				reserved: true
			),
			new PropertyInfo (
				"MSBuildProjectName",
				"Name of the project file, excluding extension.",
				reserved: true
			),
			new PropertyInfo (
				"MSBuildStartupDirectory",
				"Absolute path of the directory where MSBuild is invoked. Does not include final backslash.",
				reserved: true
			),
			new PropertyInfo (
				"MSBuildThisFile",
				"Name of the current MSBuild file, including extension.",
				reserved: true
			),
			new PropertyInfo (
				"MSBuildThisFileDirectory",
				"Directory where the current MSBuild file is located. Includes final backslash.",
				reserved: true
			),
			new PropertyInfo (
				"MSBuildThisFileDirectoryNoRoot",
				"Directory where the current MSBuild file is located, excluding drive root. Includes final backslash.",
				reserved: true
			),
			new PropertyInfo (
				"MSBuildThisFileExtension",
				"Extension of the current MSBuild file.",
				reserved: true
			),
			new PropertyInfo (
				"MSBuildThisFileFullPath",
				"Absolute path of the current MSBuild file is located.",
				reserved: true
			),
			new PropertyInfo (
				"MSBuildThisFileName",
				"Name of the current MSBuild file, excluding extension.",
				reserved: true
			),
			new PropertyInfo (
				"MSBuildToolsPath",
				"Path to the current toolset, specfied by the MSBuildToolsVersion. Does not include final backslash.",
				reserved: true
			),
			new PropertyInfo (
				"MSBuildToolsVersion",
				"Version of the current toolset.",
				reserved: true
			),
		};
	}
}