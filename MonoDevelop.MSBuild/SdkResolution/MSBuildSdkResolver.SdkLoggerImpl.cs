// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;

// this is originally from MonoDevelop - MonoDevelop.Projects.MSBuild.MSBuildSdkResolver
namespace MonoDevelop.MSBuild.SdkResolution
{
	partial class MSBuildSdkResolver
	{
		class SdkLoggerImpl : SdkLogger
		{
			readonly MSBuildContext _buildEventContext;
			readonly ILoggingService _loggingService;

			public SdkLoggerImpl (ILoggingService loggingService, MSBuildContext buildEventContext)
			{
				_loggingService = loggingService;
				_buildEventContext = buildEventContext;
			}

			public override void LogMessage (string message, MessageImportance messageImportance = MessageImportance.Low)
			{
				_loggingService.LogCommentFromText (_buildEventContext, messageImportance, message);
			}
		}
	}
}