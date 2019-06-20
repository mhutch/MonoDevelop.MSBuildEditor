// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;

using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.SdkResolution;

namespace MonoDevelop.MSBuild.Editor.Completion
{
	class ProjectCollectionRuntimeInformation : IRuntimeInformation
	{
		readonly Toolset toolset;
		readonly string binDir;
		readonly string sdksDir;
		readonly Dictionary<SdkReference, string> resolvedSdks = new Dictionary<SdkReference, string> ();
		readonly MSBuildSdkResolver sdkResolver;
		IReadOnlyDictionary<string, IReadOnlyList<string>> searchPaths;

		public ProjectCollectionRuntimeInformation (ProjectCollection projectCollection)
		{
			toolset = projectCollection.GetToolset (projectCollection.DefaultToolsVersion);
			binDir = toolset.ToolsPath;
			sdksDir = Path.GetFullPath (Path.Combine (toolset.ToolsPath, "..", "..", "Sdks"));
			sdkResolver = new MSBuildSdkResolver (binDir, sdksDir);
			searchPaths = GetImportSearchPathsTable (toolset);
		}

		public string GetBinPath () => binDir;
		public IList<SdkInfo> GetRegisteredSdks () => Array.Empty<SdkInfo> ();
		public string GetSdksPath () => sdksDir;
		public string GetToolsPath () => toolset.ToolsPath;

		public IReadOnlyDictionary<string, IReadOnlyList<string>> GetSearchPaths () => searchPaths;

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

		static IReadOnlyDictionary<string, IReadOnlyList<string>> GetImportSearchPathsTable (Toolset toolset)
		{
			var dictProp = toolset.GetType ().GetProperty ("ImportPropertySearchPathsTable", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
			var dict = (IDictionary)dictProp.GetValue (toolset);
			var importPathsType = typeof (Microsoft.Build.Evaluation.ProjectCollection).Assembly.GetType ("Microsoft.Build.Evaluation.ProjectImportPathMatch");
			var pathsField = importPathsType.GetField ("SearchPaths", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

			var converted = new Dictionary<string, IReadOnlyList<string>> ();
			var enumerator = dict.GetEnumerator ();
			while (enumerator.MoveNext ()) {
				var val = enumerator.Value != null ? (List<string>)pathsField.GetValue (enumerator.Value) : null;
				converted.Add ((string)enumerator.Key, val?.AsReadOnly ());
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