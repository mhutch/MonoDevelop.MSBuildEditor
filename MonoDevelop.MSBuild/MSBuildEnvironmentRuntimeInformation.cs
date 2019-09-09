// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using MonoDevelop.MSBuild.SdkResolution;

namespace MonoDevelop.MSBuild
{
	class MSBuildEnvironmentRuntimeInformation : IRuntimeInformation
	{
		readonly Toolset toolset;
		readonly Dictionary<SdkReference, string> resolvedSdks = new Dictionary<SdkReference, string> ();
		readonly MSBuildSdkResolver sdkResolver;

		public MSBuildEnvironmentRuntimeInformation ()
		{
			var projectCollection = ProjectCollection.GlobalProjectCollection;
			toolset = projectCollection.GetToolset (projectCollection.DefaultToolsVersion);

			var envHelperType = typeof (ProjectCollection).Assembly.GetType ("Microsoft.Build.Shared.BuildEnvironmentHelper");
			var envHelperInstanceProp = envHelperType.GetProperty ("Instance", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
			var buildEnvInstance = envHelperInstanceProp.GetValue (null);
			var buildEnvType = typeof (ProjectCollection).Assembly.GetType ("Microsoft.Build.Shared.BuildEnvironment");

			T GetEnvHelperVal<T> (string propName)
			{
				var prop = buildEnvType.GetProperty (propName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
				return (T)prop.GetValue (buildEnvInstance);
			}

			var msbuildExtensionsPath = GetEnvHelperVal<string> ("MSBuildExtensionsPath");
			SdksPath = GetEnvHelperVal<string> ("MSBuildSDKsPath");

			SearchPaths = GetImportSearchPathsTable (toolset, msbuildExtensionsPath);

			sdkResolver = new MSBuildSdkResolver (BinPath, SdksPath);
		}

		public string ToolsVersion => toolset.ToolsVersion;
		public string BinPath => toolset.ToolsPath;
		public IList<SdkInfo> GetRegisteredSdks () => Array.Empty<SdkInfo> ();
        public string SdksPath { get; }
        public string ToolsPath => toolset.ToolsPath;

        public IReadOnlyDictionary<string, IReadOnlyList<string>> SearchPaths { get; }

        public string GetSdkPath (SdkReference sdk, string projectFile, string solutionPath)
		{
			if (!resolvedSdks.TryGetValue (sdk, out string path)) {
				try {
					path = sdkResolver.GetSdkPath (sdk, new NoopLoggingService (), null, projectFile, solutionPath);
				} catch (Exception ex) {
					LoggingService.LogError ("Error in SDK resolver", ex);
				}
				resolvedSdks[sdk] = path;
			}
			return path;
		}

		static IReadOnlyDictionary<string, IReadOnlyList<string>> GetImportSearchPathsTable (Toolset toolset, string msbuildExtensionsPath)
		{
			var dictProp = toolset.GetType ().GetProperty ("ImportPropertySearchPathsTable", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
			var dict = (IDictionary)dictProp.GetValue (toolset);
			var importPathsType = typeof (ProjectCollection).Assembly.GetType ("Microsoft.Build.Evaluation.ProjectImportPathMatch");
			var pathsField = importPathsType.GetField ("SearchPaths", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

			var converted = new Dictionary<string, IReadOnlyList<string>> ();
			var enumerator = dict.GetEnumerator ();
			while (enumerator.MoveNext ()) {
				if (enumerator.Value == null) {
					continue;
				}
				var key = (string)enumerator.Key;
				var val = (List<string>)pathsField.GetValue (enumerator.Value);

				if (key == "MSBuildExtensionsPath" || key == "MSBuildExtensionsPath32" || key == "MSBuildExtensionsPath64") {
					var oldVal = val;
					val = new List<string> (oldVal.Count + 1) { msbuildExtensionsPath };
					val.AddRange (oldVal);
				}

				converted.Add (key, val.AsReadOnly ());
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