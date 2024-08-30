// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Threading;

using Microsoft.Extensions.Logging;

using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.SdkResolution;

namespace MonoDevelop.MSBuild.Editor.Completion;

internal static class SdkCompletion
{
	//FIXME: SDK version completion
	//FIXME: enumerate SDKs from NuGet
	public static List<SdkInfo> GetSdkCompletions (MSBuildRootDocument doc, ILogger logger, CancellationToken token)
	{
		var items = new List<SdkInfo> ();
		var sdks = new HashSet<string> ();

		foreach (var sdk in doc.Environment.GetRegisteredSdks ()) {
			if (sdks.Add (sdk.Name)) {
				items.Add (sdk);
			}
		}

		//FIXME we should be able to cache these
		doc.Environment.ToolsetProperties.TryGetValue (WellKnownProperties.MSBuildSDKsPath, out var sdksPath);
		if (sdksPath != null) {
			AddSdksFromDir (sdksPath);
		}

		var dotNetSdk = doc.Environment.ResolveSdk (new ("Microsoft.NET.Sdk", null, null), null, null, logger);
		if (dotNetSdk?.Path is string sdkPath) {
			string? dotNetSdkPath = Path.GetDirectoryName (Path.GetDirectoryName (sdkPath));
			if (dotNetSdkPath is not null && (sdksPath is null || Path.GetFullPath (dotNetSdkPath) != Path.GetFullPath (sdksPath))) {
				AddSdksFromDir (dotNetSdkPath);
			}
		}

		void AddSdksFromDir (string sdkDir)
		{
			if (!Directory.Exists (sdkDir)) {
				return;
			}
			foreach (var dir in Directory.GetDirectories (sdkDir)) {
				string name = Path.GetFileName (dir);
				var targetsFileExists = File.Exists (Path.Combine (dir, "Sdk", "Sdk.targets"));
				if (targetsFileExists && sdks.Add (name)) {
					items.Add (new SdkInfo (name, null, Path.Combine (dir, name)));
				}
			}
		}

		return items;
	}
}
