// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;

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
		readonly object _lockObject = new ();
		IList<SdkResolver> _resolvers;

		readonly IMSBuildEnvironment msbuildEnvironment;

		internal MSBuildSdkResolver (IMSBuildEnvironment environment)
		{
			this.msbuildEnvironment = environment;
		}

		// helpers for imported code
		string SDKsPath => msbuildEnvironment.TryGetToolsetProperty (ReservedProperties.SDKsPath, out var sdksPath) ? sdksPath : null;
		string ToolsPath32 => msbuildEnvironment.TryGetToolsetProperty (ReservedProperties.ToolsPath32, out var toolsPath32)? toolsPath32 : msbuildEnvironment.ToolsPath;

		/// <summary>
		///     Get path on disk to the referenced SDK.
		/// </summary>
		/// <param name="sdk">SDK referenced by the Project.</param>
		/// <param name="logger">Logging service.</param>
		/// <param name="buildEventContext">Build event context for logging.</param>
		/// <param name="projectFile">Location of the element within the project which referenced the SDK.</param>
		/// <param name="solutionPath">Path to the solution if known.</param>
		/// <returns>Path to the root of the referenced SDK.</returns>
		internal SdkInfo ResolveSdk (SdkReference sdk, ILoggingService logger, MSBuildContext buildEventContext,
			string projectFile, string solutionPath)
		{
			if (_resolvers == null) Initialize (logger);

			var results = new List<SdkResultImpl> ();

			try {
				var buildEngineLogger = new SdkLoggerImpl (logger, buildEventContext);
				foreach (var sdkResolver in _resolvers) {
					var context = new SdkResolverContextImpl (buildEngineLogger, projectFile, solutionPath, msbuildEnvironment.EngineVersion);
					var resultFactory = new SdkResultFactoryImpl (sdk);
					try {
						var result = (SdkResultImpl)sdkResolver.Resolve (sdk, context, resultFactory);
						if (result != null && result.Success) {
							LogWarnings (logger, buildEventContext, projectFile, result);
							return new SdkInfo(sdk.Name, result);
						}

						if (result != null)
							results.Add (result);
					} catch (Exception e) {
						logger.LogFatalBuildError (buildEventContext, e, projectFile);
					}
				}
			} catch (Exception e) {
				logger.LogFatalBuildError (buildEventContext, e, projectFile);
				throw;
			}

			foreach (var result in results) {
				LogWarnings (logger, buildEventContext, projectFile, result);

				if (result.Errors != null) {
					foreach (var error in result.Errors) {
						logger.LogErrorFromText (buildEventContext, subcategoryResourceName: null, errorCode: null,
							helpKeyword: null, file: projectFile, message: error);
					}
				}
			}

			return null;
		}

		void Initialize (ILoggingService logger)
		{
			lock (_lockObject) {
				if (_resolvers != null) return;
				_resolvers = LoadResolvers (logger, logger);
			}
		}

		static void LogWarnings (ILoggingService loggingContext, MSBuildContext bec, string projectFile, SdkResultImpl result)
		{
			if (result.Warnings == null) return;

			foreach (var warning in result.Warnings)
				loggingContext.LogWarningFromText (bec, null, null, null, projectFile, warning);
		}
	}
}