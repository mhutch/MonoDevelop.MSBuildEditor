// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MonoDevelop.MSBuild
{
	static class WellKnownProperties
	{
		public const string FrameworkSDKRoot = nameof (FrameworkSDKRoot);
		public const string InnerBuildProperty = nameof (InnerBuildProperty);
		public const string InnerBuildPropertyValues = nameof (InnerBuildPropertyValues);
		public const string IsGraphBuild = nameof (IsGraphBuild);
		public const string MSBuildExtensionsPath = nameof (MSBuildExtensionsPath);
		public const string MSBuildExtensionsPath32 = nameof (MSBuildExtensionsPath32);
		public const string MSBuildExtensionsPath64 = nameof (MSBuildExtensionsPath64);
		public const string MSBuildFrameworkToolsPath = nameof (MSBuildFrameworkToolsPath);
		public const string MSBuildFrameworkToolsPath32 = nameof (MSBuildFrameworkToolsPath32);
		public const string MSBuildFrameworkToolsPath64 = nameof (MSBuildFrameworkToolsPath64);
		public const string MSBuildOverrideTasksPath = nameof (MSBuildOverrideTasksPath);
		public const string MSBuildSDKsPath = nameof (MSBuildSDKsPath);
		public const string MSBuildToolsPath32 = nameof (MSBuildToolsPath32);
		public const string MSBuildToolsPath64 = nameof (MSBuildToolsPath64);
		public const string MSBuildUserExtensionsPath = nameof (MSBuildUserExtensionsPath);
		public const string MSBuildWarningsAsErrors = nameof (MSBuildWarningsAsErrors);
		public const string MSBuildWarningsAsMessages = nameof (MSBuildWarningsAsMessages);
		public const string MSBuildWarningsNotAsErrors = nameof (MSBuildWarningsNotAsErrors);
		public const string RoslynTargetsPath = nameof (RoslynTargetsPath);
		public const string SDK35ToolsPath = nameof (SDK35ToolsPath);
		public const string SDK40ToolsPath = nameof (SDK40ToolsPath);
		public const string VsInstallRoot = nameof (VsInstallRoot);
		public const string WindowsSDK80Path = nameof (WindowsSDK80Path);
		public const string VisualStudioVersion = nameof (VisualStudioVersion);
		public const string MSBuildProgramFiles32 = nameof (MSBuildProgramFiles32);
		public const string MSBuildProgramFiles64 = nameof (MSBuildProgramFiles64);
		public const string MSBuildProjectExtensionsPath = nameof (MSBuildProjectExtensionsPath);
		public const string MSBuildTreatWarningsAsErrors = nameof (MSBuildTreatWarningsAsErrors);
		public const string MSBuildAllProjects = nameof (MSBuildAllProjects);
	}
}