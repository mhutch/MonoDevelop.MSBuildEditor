// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.Collections.Generic;

using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging;

using ILogger = Microsoft.Extensions.Logging.ILogger;

// this is originally from MonoDevelop - MonoDevelop.Projects.MSBuild.MSBuildSdkResolver
//
// the portions that MonoDevelop had imported from the internal MSBuild class Microsoft.Build.BackEnd.SdkResolution.SdkResolverLoader
// have been moved to a partial class in the file MSBuildSdkResolver.Imported.cs

namespace MonoDevelop.MSBuild.SdkResolution
{
	/// <summary>
	///     Component responsible for resolving an SDK to a file path. Loads and coordinates
	///     with <see cref="SdkResolver" /> plug-ins.
	/// </summary>
	partial class MSBuildSdkResolver
	{
		readonly Lazy<IList<SdkResolver>> resolvers;
		readonly IMSBuildEnvironment msbuildEnvironment;
		readonly ILogger environmentLogger;

		internal MSBuildSdkResolver (IMSBuildEnvironment environment, ILogger environmentLogger)
		{
			Microsoft.Build.Shared.BuildEnvironmentHelper.EnsureInitialized (environment);

			this.msbuildEnvironment = environment;
			this.environmentLogger = environmentLogger;
			this.resolvers = new (() => LoadResolvers (environmentLogger));
		}

		// helpers for imported code
		string? SDKsPath => msbuildEnvironment.ToolsetProperties.TryGetValue (WellKnownProperties.MSBuildSDKsPath, out var sdksPath) ? sdksPath : null;
		string? ToolsPath32 => msbuildEnvironment.ToolsetProperties.TryGetValue (WellKnownProperties.MSBuildToolsPath32, out var toolsPath32)? toolsPath32 : msbuildEnvironment.ToolsPath;

		/// <summary>
		///     Get path on disk to the referenced SDK.
		/// </summary>
		/// <param name="sdk">SDK referenced by the Project.</param>
		/// <param name="projectFile">Location of the element within the project which referenced the SDK.</param>
		/// <param name="solutionPath">Path to the solution if known.</param>
		/// <returns>Path to the root of the referenced SDK.</returns>
		internal SdkInfo? ResolveSdk (SdkReference sdk, string projectFile, string? solutionPath, ILogger logger)
		{
			using var logScope = logger.BeginScope (projectFile);

			// errors specific to this call go to the logger we were pass if possible, else to the env logger
			logger ??= environmentLogger;

			var results = new List<SdkResultImpl> ();

			var buildEngineLogger = new SdkLoggerImpl (logger);
			foreach (var sdkResolver in resolvers.Value) {
				var context = new SdkResolverContextImpl (buildEngineLogger, projectFile, solutionPath, msbuildEnvironment.EngineVersion);
				var resultFactory = new SdkResultFactoryImpl (sdk);
				try {
					var result = (SdkResultImpl)sdkResolver.Resolve (sdk, context, resultFactory);
					if (result is null) {
						continue;
					}
					LogResolverMessages (logger, LogLevel.Warning, result.Warnings);
					LogResolverMessages (logger, LogLevel.Error, result.Errors);
					if (result.Success) {
						return new SdkInfo(sdk.Name, result);
					}
				} catch (Exception ex) {
					LogUnhandledSdkResolverError (logger, ex, sdkResolver.Name);
				}
			}

			return null;
		}

		static void LogResolverMessages (ILogger logger, LogLevel logLevel, IEnumerable<string>? messages)
		{
			if (messages is null || !logger.IsEnabled(logLevel)) {
				return;
			}
			foreach (var message in messages) {
				if (message is not null && !ShouldIgnoreMessage (message)) {
					logger.Log (logLevel, SdkResolverLogMessageId, message);
				}
			}
		}

		static bool ShouldIgnoreMessage (string message)
			=> string.Equals (message, "The NuGetSdkResolver did not resolve this SDK because there was no version specified in the project or global.json.", StringComparison.Ordinal)
			|| message.EndsWith ("Check that a recent enough .NET SDK is installed and/or increase the version specified in global.json.", StringComparison.Ordinal);

		[LoggerMessage (EventId = 0, Level = LogLevel.Error, Message = "Unhandled error in SDK resolver '{resolverName}'")]
		static partial void LogUnhandledSdkResolverError (ILogger logger, Exception ex, string resolverName);

		static EventId SdkResolverLogMessageId = new (1, "SdkResolverLogMessage");
	}
}