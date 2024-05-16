// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP
#nullable enable
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging;

using MonoDevelop.MSBuild.SdkResolution;

using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace MonoDevelop.MSBuild
{
	/// <summary>
	/// Describes the MSBuild environment of the current process
	/// </summary>
	partial class CurrentProcessMSBuildEnvironment : IMSBuildEnvironment
	{
		readonly Toolset toolset;
		readonly Dictionary<SdkReference, SdkInfo?> resolvedSdks = new();
		readonly MSBuildSdkResolver sdkResolver;
		readonly ILogger logger;

		public CurrentProcessMSBuildEnvironment (ILogger logger)
		{
			var projectCollection = ProjectCollection.GlobalProjectCollection;
			toolset = projectCollection.GetToolset (projectCollection.DefaultToolsVersion);

			ToolsetProperties = GetToolsetProperties (toolset);
			ProjectImportSearchPaths = GetImportSearchPaths (toolset, logger);

			EnvironmentVariables = GetEnvironmentVariables ();

			sdkResolver = new MSBuildSdkResolver (this, logger);
			this.logger = logger;
		}

		public string ToolsVersion => toolset.ToolsVersion;
		public string? ToolsPath => toolset.ToolsPath;

		public IList<SdkInfo> GetRegisteredSdks () => Array.Empty<SdkInfo> ();

		public Version EngineVersion => ProjectCollection.Version;

		public IReadOnlyDictionary<string, string> ToolsetProperties { get; }

		public IReadOnlyDictionary<string, string> EnvironmentVariables { get; init; }

		public IReadOnlyDictionary<string, string[]> ProjectImportSearchPaths { get; }

		//FIXME: caching should be specific to the (projectFile, string solutionPath) pair
		public virtual SdkInfo? ResolveSdk (MSBuildSdkReference sdk, string projectFile, string? solutionPath, ILogger logger)
		{
			var sdkRef =  sdk.AsSdkReference ();
			if (!resolvedSdks.TryGetValue (sdkRef, out SdkInfo? sdkInfo)) {
				try {
					sdkInfo = sdkResolver.ResolveSdk (sdkRef, projectFile, solutionPath, logger ?? this.logger);
				} catch (Exception ex) {
					LogUnhandledErrorInSdkResolver (logger, ex);
				}
				resolvedSdks[sdkRef] = sdkInfo;
			}
			return sdkInfo;
		}

		static Dictionary<string, string> GetToolsetProperties (Toolset toolset)
		{
			var toolsetProperties = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase);

			foreach (var prop in toolset.Properties) {
				var propVal = prop.Value.EvaluatedValue;
				toolsetProperties.Add (prop.Key, propVal);
			}

			return toolsetProperties;
		}

		static Dictionary<string, string> GetEnvironmentVariables ()
		{
			var environmentVariables = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase);

			foreach (DictionaryEntry envVar in Environment.GetEnvironmentVariables ()) {
				if (envVar.Value is string value) {
					environmentVariables.Add ((string)envVar.Key, value);
				}
			}

			return environmentVariables;
		}

		static Dictionary<string, string[]> GetImportSearchPaths (Toolset toolset, ILogger logger)
		{
			var converted = new Dictionary<string, string[]> (StringComparer.OrdinalIgnoreCase);

			try {
				var dictProp = toolset.GetType ().GetProperty ("ImportPropertySearchPathsTable", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
				if (dictProp is null) {
					LogToolsetSearchPathsErrorWithMessage (logger, "could not get ImportPropertySearchPathsTable property");
					return converted;
				}

				var dict = (IDictionary?)dictProp.GetValue (toolset);
				if (dict is null) {
					LogToolsetSearchPathsErrorWithMessage (logger, "ImportPropertySearchPathsTable property value is not IDictionary");
					return converted;
				}

				var importPathsType = typeof (ProjectCollection).Assembly.GetType ("Microsoft.Build.Evaluation.ProjectImportPathMatch");
				if (importPathsType is null) {
					LogToolsetSearchPathsErrorWithMessage (logger, "could not get ProjectImportPathMatch type");
					return converted;
				}

				var pathsField = importPathsType.GetField ("SearchPaths", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
					// in MSBuild15 'SearchPaths' is a public property, and '_searchPaths' is the backing field
					?? importPathsType.GetField ("_searchPaths", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
				if (pathsField is null) {
					LogToolsetSearchPathsErrorWithMessage (logger, "could not get SearchPaths or _searchPaths fields");
					return converted;
				}

				var enumerator = dict.GetEnumerator ();
				while (enumerator.MoveNext ()) {
					var key = (string)enumerator.Key;
					if (pathsField.GetValue (enumerator.Value) is List<string> val) {
						converted.Add (key, val.ToArray ());
					} else {
						LogToolsetSearchPathsErrorWithMessage (logger, $"search path '{key}' value not convertible to List<string>");
					}
				}
			} catch (Exception ex) {
				LogToolsetSearchPathsUnhandledError (logger, ex);
			}

			return converted;
		}

		[LoggerMessage (EventId = 0, Level = LogLevel.Error, Message = "Unhandled error in SDK resolver")]
		static partial void LogUnhandledErrorInSdkResolver (ILogger logger, Exception ex);

		[LoggerMessage (EventId = 1, Level = LogLevel.Error, Message = "Unhandled error getting toolset search paths")]
		static partial void LogToolsetSearchPathsUnhandledError (ILogger logger, Exception ex);

		[LoggerMessage (EventId = 2, Level = LogLevel.Error, Message = "Error getting toolset search paths: {message}")]
		static partial void LogToolsetSearchPathsErrorWithMessage (ILogger logger, string message);
	}
}