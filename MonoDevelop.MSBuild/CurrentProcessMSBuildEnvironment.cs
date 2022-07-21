// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;

using MonoDevelop.MSBuild.Evaluation;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.SdkResolution;

namespace MonoDevelop.MSBuild
{
	/// <summary>
	/// Describes the MSBuild environment of the current process
	/// </summary>
	class CurrentProcessMSBuildEnvironment : IMSBuildEnvironment
	{
		readonly Toolset toolset;
		readonly Dictionary<SdkReference, SdkInfo> resolvedSdks = new();
		readonly MSBuildSdkResolver sdkResolver;

		public CurrentProcessMSBuildEnvironment ()
		{
			var projectCollection = ProjectCollection.GlobalProjectCollection;
			toolset = projectCollection.GetToolset (projectCollection.DefaultToolsVersion);

			ToolsetProperties = GetToolsetProperties (toolset);
			ProjectImportSearchPaths = GetImportSearchPaths (toolset);

			sdkResolver = new MSBuildSdkResolver (this);
		}

		public string ToolsVersion => toolset.ToolsVersion;
        public string ToolsPath => toolset.ToolsPath;

		public IList<SdkInfo> GetRegisteredSdks () => Array.Empty<SdkInfo> ();

		public Version EngineVersion => ProjectCollection.Version;

		public IReadOnlyDictionary<string, string> ToolsetProperties { get; }

		public IReadOnlyDictionary<string, string[]> ProjectImportSearchPaths { get; }

		//FIXME: caching should be specific to the (projectFile, string solutionPath) pair
		public SdkInfo ResolveSdk (
			(string name, string version, string minimumVersion) sdk, string projectFile, string solutionPath)
		{
			var sdkRef = new SdkReference (sdk.name, sdk.version, sdk.minimumVersion);
			if (!resolvedSdks.TryGetValue (sdkRef, out SdkInfo sdkInfo)) {
				try {
					//FIXME: capture errors & warnings from logger and return those too?
					// FIX THIS, at least log to the static logger
					sdkInfo = sdkResolver.ResolveSdk (sdkRef, new NoopLoggingService (), null, projectFile, solutionPath);
				} catch (Exception ex) {
					LoggingService.LogError ("Error in SDK resolver", ex);
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

		static Dictionary<string, string[]> GetImportSearchPaths (Toolset toolset)
		{
			var dictProp = toolset.GetType ().GetProperty ("ImportPropertySearchPathsTable", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
			var dict = (IDictionary)dictProp.GetValue (toolset);
			var importPathsType = typeof (ProjectCollection).Assembly.GetType ("Microsoft.Build.Evaluation.ProjectImportPathMatch");
			var pathsField = importPathsType.GetField ("SearchPaths", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

			var converted = new Dictionary<string, string[]> (StringComparer.OrdinalIgnoreCase);
			var enumerator = dict.GetEnumerator ();
			while (enumerator.MoveNext ()) {
				if (enumerator.Value == null) {
					continue;
				}
				var key = (string)enumerator.Key;
				var val = (List<string>)pathsField.GetValue (enumerator.Value);
				converted.Add (key, val.ToArray ());
			}
			return converted;
		}

		class NoopLoggingService : ILoggingService
		{
			public void LogCommentFromText (MSBuildContext buildEventContext, MessageImportance messageImportance, string message)
			{
			}

			public void LogErrorFromText (MSBuildContext buildEventContext, object subcategoryResourceName, object errorCode, object helpKeyword, string file, string message)
			{
			}

			public void LogFatalBuildError (MSBuildContext buildEventContext, Exception e, string projectFile)
			{
			}

			public void LogWarning (string message)
			{
			}

			public void LogWarningFromText (MSBuildContext bec, object p1, object p2, object p3, string projectFile, string warning)
			{
			}
		}
	}
}