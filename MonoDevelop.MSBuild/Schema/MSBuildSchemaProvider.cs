// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;

namespace MonoDevelop.MSBuild.Schema
{
	class MSBuildSchemaProvider
	{
		public virtual MSBuildSchema GetSchema (string path, string sdk)
		{
			string filename = path + ".buildschema.json";
			if (File.Exists (filename)) {
				using (var reader = File.OpenText (filename)) {
					return MSBuildSchema.Load (reader);
				}
			}

			var resourceId = GetResourceForBuiltin (path, sdk);
			if (resourceId != null) {
				return MSBuildSchema.LoadResource ($"MonoDevelop.MSBuildEditor.Schemas.{resourceId}.buildschema.json");
			}

			return null;
		}

		static string GetResourceForBuiltin (string filepath, string sdkId)
		{
			switch (Path.GetFileName (filepath).ToLower ()) {
			case "microsoft.common.targets":
				return "CommonTargets";
			case "microsoft.codeanalysis.targets":
				return "CodeAnalysis";
			case "microsoft.visualbasic.currentversion.targets":
				return "VisualBasic";
			case "microsoft.csharp.currentversion.targets":
				return "CSharp";
			case "microsoft.cpp.targets":
				return "Cpp";
			case "nuget.build.tasks.pack.targets":
				return "NuGetPack";
			case "sdk.targets":
				switch (sdkId) {
				case "microsoft.net.sdk":
					return "NetSdk";
				}
				break;
			}
			return null;
		}
	}
}
