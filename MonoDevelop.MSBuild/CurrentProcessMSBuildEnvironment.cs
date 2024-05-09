// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
		readonly Dictionary<SdkReference, SdkInfo> resolvedSdks = new();
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
        public string ToolsPath => toolset.ToolsPath;

		public IList<SdkInfo> GetRegisteredSdks () => Array.Empty<SdkInfo> ();

		public Version EngineVersion => ProjectCollection.Version;

		public IReadOnlyDictionary<string, string> ToolsetProperties { get; }

		public IReadOnlyDictionary<string, string> EnvironmentVariables { get; init; }

		public IReadOnlyDictionary<string, string[]> ProjectImportSearchPaths { get; }

		//FIXME: caching should be specific to the (projectFile, string solutionPath) pair
		public virtual SdkInfo ResolveSdk (MSBuildSdkReference sdk, string projectFile, string solutionPath, ILogger logger = null)
		{
			var sdkRef =  sdk.AsSdkReference ();
			if (!resolvedSdks.TryGetValue (sdkRef, out SdkInfo sdkInfo)) {
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
				environmentVariables.Add ((string)envVar.Key, (string)envVar.Value);
			}

			return environmentVariables;
		}

		static Dictionary<string, string[]> GetImportSearchPaths (Toolset toolset, ILogger logger)
		{
			var converted = new Dictionary<string, string[]> (StringComparer.OrdinalIgnoreCase);

			try {
				var dictProp = toolset.GetType ().GetProperty ("ImportPropertySearchPathsTable", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
				var dict = (IDictionary)dictProp.GetValue (toolset);
				var importPathsType = typeof (ProjectCollection).Assembly.GetType ("Microsoft.Build.Evaluation.ProjectImportPathMatch");
				var pathsField = importPathsType.GetField ("SearchPaths", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
					// in MSBuild15 'SearchPaths' is a public property, and '_searchPaths' is the backing field
					?? importPathsType.GetField ("_searchPaths", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

				var enumerator = dict.GetEnumerator ();
				while (enumerator.MoveNext ()) {
					if (enumerator.Value == null) {
						continue;
					}
					var key = (string)enumerator.Key;
					var val = (List<string>)pathsField.GetValue (enumerator.Value);
					converted.Add (key, val.ToArray ());
				}
			} catch (Exception ex) {
				LogUnhandledErrorInToolsetSearchPaths (logger, ex);
			}

			return converted;
		}

		[LoggerMessage (EventId = 0, Level = LogLevel.Error, Message = "Unhandled error in SDK resolver")]
		static partial void LogUnhandledErrorInSdkResolver (ILogger logger, Exception ex);

		[LoggerMessage (EventId = 1, Level = LogLevel.Error, Message = "Unhandled error getting toolset search paths")]
		static partial void LogUnhandledErrorInToolsetSearchPaths (ILogger logger, Exception ex);
	}
}