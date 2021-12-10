// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using MonoDevelop.Xml.Parser;

namespace MonoDevelop.MSBuild.Schema
{
	class MSBuildSchemaProvider
	{
		public MSBuildSchema GetSchema (string path, string sdk)
		{
			var schema = GetSchema (path, sdk, out var loadErrors);

			// FIXME: log which the error came from
			if (loadErrors != null) {
				foreach (var error in loadErrors) {
					if (error.Severity == DiagnosticSeverity.Warning) {
						LoggingService.LogWarning (error.Message);
					} else {
						LoggingService.LogError (error.Message);
					}
				}
			}

			return schema;
		}

		public virtual MSBuildSchema GetSchema (string path, string sdk, out IList<MSBuildSchemaLoadError> loadErrors)
		{
			string filename = path + ".buildschema.json";
			if (File.Exists (filename)) {
				using (var reader = File.OpenText (filename)) {
					return MSBuildSchema.Load (reader, out loadErrors, filename);
				}
			}

			return GetResourceForBuiltin (path, sdk, out loadErrors);
		}

		// don't inline this, MSBuildSchema.LoadResource gets the calling assembly
		[MethodImpl (MethodImplOptions.NoInlining)]
		static MSBuildSchema GetResourceForBuiltin (string filepath, string sdkId, out IList<MSBuildSchemaLoadError> loadErrors)
		{
			var resourceId = GetResourceIdForBuiltin (filepath, sdkId);
			if (resourceId != null) {
				return MSBuildSchema.LoadResourceFromCallingAssembly ($"MonoDevelop.MSBuild.Schemas.{resourceId}.buildschema.json", out loadErrors);
			}
			loadErrors = null;
			return null;
		}

		static string GetResourceIdForBuiltin (string filepath, string sdkId)
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
				switch (sdkId?.ToLower()) {
				case "microsoft.net.sdk":
					return "NetSdk";
				}
				break;
			}
			return null;
		}
	}
}
