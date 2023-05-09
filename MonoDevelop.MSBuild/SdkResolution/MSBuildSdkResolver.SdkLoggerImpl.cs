// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging;

using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace MonoDevelop.MSBuild.SdkResolution;

partial class MSBuildSdkResolver
{
	/// <summary>
	/// Implementation of <see cref="SdkLogger"/> that routes to an <see cref="ILogger"/>
	/// </summary>
	class SdkLoggerImpl : SdkLogger
	{
		readonly ILogger logger;

		public SdkLoggerImpl (ILogger logger)
		{
			this.logger = logger;
		}

		public override void LogMessage (string message, MessageImportance messageImportance = MessageImportance.Low)
		{
			var mappedLevel = MapLogLevel (messageImportance);
			if (logger.IsEnabled (mappedLevel)) {
				logger.Log (mappedLevel, SdkResolverMessageId, message);
			}
		}

		readonly EventId SdkResolverMessageId = new (0, "SdkResolver");

		static LogLevel MapLogLevel (MessageImportance messageImportance) => messageImportance switch {
			MessageImportance.Normal => LogLevel.Information,
			MessageImportance.Low => LogLevel.Debug,
			MessageImportance.High => LogLevel.Warning,
			_ => throw new ArgumentOutOfRangeException (nameof (messageImportance))
		};
	}
}