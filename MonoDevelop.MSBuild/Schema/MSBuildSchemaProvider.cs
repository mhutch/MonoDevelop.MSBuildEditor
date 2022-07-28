// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
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

		static MSBuildSchema GetResourceForBuiltin (string filepath, string sdkId, out IList<MSBuildSchemaLoadError> loadErrors)
		{
			var resourceId = GetResourceIdForBuiltin (filepath, sdkId);
			if (resourceId != null) {
				return LoadBuiltinSchema (resourceId, out loadErrors);
			}
			loadErrors = null;
			return null;
		}

		// don't inline this, MSBuildSchema.LoadResource gets the calling assembly
		[MethodImpl (MethodImplOptions.NoInlining)]
		static MSBuildSchema LoadBuiltinSchema (string resourceId, out IList<MSBuildSchemaLoadError> loadErrors)
			=> MSBuildSchema.LoadResourceFromCallingAssembly ($"MonoDevelop.MSBuild.Schemas.{resourceId}.buildschema.json", out loadErrors);

		static string GetResourceIdForBuiltin (string filepath, string sdkId)
			=> (Path.GetFileName (filepath).ToLower ()) switch {
				"microsoft.common.targets" => BuiltinSchema.CommonTargets,
				"microsoft.codeanalysis.targets" => BuiltinSchema.CodeAnalysis,
				"microsoft.visualbasic.currentversion.targets" => BuiltinSchema.VisualBasic,
				"microsoft.csharp.currentversion.targets" => BuiltinSchema.CSharp,
				"microsoft.cpp.targets" => BuiltinSchema.Cpp,
				"nuget.build.tasks.pack.targets" => BuiltinSchema.NuGetPack,
				"sdk.targets" => sdkId?.ToLower () switch {
					"microsoft.net.sdk" => BuiltinSchema.NetSdk,
					_ => null
				},
				_ => null
			};

		internal static IEnumerable<(MSBuildSchema schema, IList<MSBuildSchemaLoadError> errors)> GetAllBuiltinSchemas ()
			=> new string[] {
				BuiltinSchema.CommonTargets,
				BuiltinSchema.CodeAnalysis,
				BuiltinSchema.VisualBasic,
				BuiltinSchema.CSharp,
				BuiltinSchema.Cpp,
				BuiltinSchema.NuGetPack,
				BuiltinSchema.NetSdk
			}
			.Select (s => (LoadBuiltinSchema (s, out var e), e));

		class BuiltinSchema
		{
			public const string CommonTargets = nameof (CommonTargets);
			public const string CodeAnalysis = nameof (CodeAnalysis);
			public const string VisualBasic = nameof (VisualBasic);
			public const string CSharp = nameof (CSharp);
			public const string Cpp = nameof (Cpp);
			public const string NuGetPack = nameof (NuGetPack);
			public const string NetSdk = nameof (NetSdk);
		}
	}
}
