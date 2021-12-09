// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MonoDevelop.MSBuild
{
	static class ReservedProperties
	{
		public const string BinPath = "MSBuildBinPath";
		public const string ToolsPath = "MSBuildToolsPath";
		public const string ToolsPath32 = "MSBuildToolsPath32";
		public const string ToolsPath64 = "MSBuildToolsPath64";
		public const string SDKsPath = "MSBuildSDKsPath";
		public const string ToolsVersion = "MSBuildToolsVersion";
		public const string ProgramFiles32 = "MSBuildProgramFiles32";
		public const string ProgramFiles64 = "MSBuildProgramFiles64";

		// technically well-known, not reserved
		public const string ExtensionsPath = "MSBuildExtensionsPath";
		public const string ExtensionsPath32 = "MSBuildExtensionsPath32";
		public const string ExtensionsPath64 = "MSBuildExtensionsPath64";

		public const string RoslynTargetsPath = "RoslynTargetsPath";
		public const string VisualStudioVersion = "VisualStudioVersion";

		public const string ThisFile = "MSBuildThisFile";
		public const string ThisFileDirectory = "MSBuildThisFileDirectory";
		public const string ThisFileExtension = "MSBuildThisFileExtension";
		public const string ThisFileFullPath = "MSBuildThisFileFullPath";
		public const string ThisFileName = "MSBuildThisFileName";

		public const string ProjectDirectory = "MSBuildProjectDirectory";
		public const string ProjectDirectoryNoRoot = "MSBuildProjectDirectoryNoRoot";
		public const string ProjectExtension = "MSBuildProjectExtension";
		public const string ProjectFile = "MSBuildProjectFile";
		public const string ProjectFullPath = "MSBuildProjectFullPath";
		public const string ProjectName = "MSBuildProjectName";
		public const string StartupDirectory = "MSBuildStartupDirectory";
		public const string ProjectExtensionsPath = "MSBuildProjectExtensionsPath";
	}
}