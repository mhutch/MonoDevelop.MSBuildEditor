// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using Microsoft.Build.Framework;

// this is originally from MonoDevelop - MonoDevelop.Projects.MSBuild.MSBuildSdkResolver
namespace MonoDevelop.MSBuild.SdkResolution
{
	partial class MSBuildSdkResolver
	{
		sealed class SdkResolverContextImpl : SdkResolverContext
		{
			public SdkResolverContextImpl (SdkLogger logger, string projectFilePath, string? solutionPath, Version msbuildVersion)
			{
				Logger = logger;
				ProjectFilePath = projectFilePath;
				SolutionFilePath = solutionPath;
				MSBuildVersion = msbuildVersion;
			}
		}
	}
}