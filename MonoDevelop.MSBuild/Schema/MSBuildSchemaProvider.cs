// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

using Microsoft.Extensions.Logging;

namespace MonoDevelop.MSBuild.Schema
{
	class MSBuildSchemaProvider
	{
		public MSBuildSchema? GetSchema (string path, string sdk, ILogger logger)
		{
			var schema = GetSchema (path, sdk, out var loadErrors);

			// FIXME: log which schema the error came from
			if (loadErrors != null) {
				using var logScope = logger.BeginScope (path);
				foreach (var error in loadErrors) {
					var logLevel = error.Severity == Xml.Analysis.XmlDiagnosticSeverity.Warning ? LogLevel.Warning : LogLevel.Error;
					if (logger.IsEnabled (logLevel)) {
						logger.Log (logLevel, schemaLoadErrorId, null, error.Message);
					}
				}
			}

			return schema;
		}

		static readonly EventId schemaLoadErrorId = new (0, "SchemaLoadError");

		public virtual MSBuildSchema? GetSchema (string path, string sdk, out IList<MSBuildSchemaLoadError>? loadErrors)
		{
			string filename = path + ".buildschema.json";
			if (File.Exists (filename)) {
				using var reader = File.OpenText (filename);
				return MSBuildSchema.Load (reader, out loadErrors, filename);
			}

			return GetResourceIdForBuiltInSchema (path, sdk, out loadErrors);
		}

		static MSBuildSchema? GetResourceIdForBuiltInSchema (string filePath, string sdkId, out IList<MSBuildSchemaLoadError>? loadErrors)
		{
			if (GetResourceIdForBuiltInSchema (filePath, sdkId) is string resourceId) {
				return LoadBuiltInSchema (resourceId, out loadErrors);
			}
			loadErrors = null;
			return null;
		}

		// don't inline this, MSBuildSchema.LoadResource gets the calling assembly
		[MethodImpl (MethodImplOptions.NoInlining)]
		static MSBuildSchema LoadBuiltInSchema (string resourceId, out IList<MSBuildSchemaLoadError> loadErrors)
			=> MSBuildSchema.LoadResourceFromCallingAssembly ($"MonoDevelop.MSBuild.Schemas.{resourceId}.buildschema.json", out loadErrors);

		static string? GetResourceIdForBuiltInSchema (string filePath, string sdkId) => builtInSchemaMap.TryGetValue(new (sdkId, Path.GetFileName (filePath)), out var resourceId) ? resourceId : null;

		internal static IEnumerable<(MSBuildSchema schema, IList<MSBuildSchemaLoadError> errors)> GetAllBuiltInSchemas ()
			=> builtInSchemas.Select (s => (LoadBuiltInSchema (s.resourceId, out var e), e));

		const string sdkTargets = "sdk.targets";

		static readonly (string resourceId, string? sdkId, string filename)[] builtInSchemas = new[] {
			("Android", null, "Xamarin.Android.Common.targets"),
			("Appx", null, "Microsoft.DesktopBridge.targets"),
			("AspNetCore", "Microsoft.NET.Sdk.Web", sdkTargets),
			("CodeAnalysis", null, "Microsoft.CodeAnalysis.targets"),
			("CommonTargets", null, "Microsoft.Common.targets"),
			("Cpp", null, "Microsoft.Cpp.targets"),
			("CSharp", null, "Microsoft.CSharp.CurrentVersion.targets"),
			("NetSdk", "Microsoft.NET.Sdk", sdkTargets),
			("NuGet", null, "NuGet.targets"),
			("NuGetPack", null, "NuGet.Build.Tasks.Pack.targets"),
			("GrpcProtobuf", null, "Google.Protobuf.Tools.targets"),
			("RazorSdk", "Microsoft.NET.Sdk.Razor", sdkTargets),
			("Roslyn", null, "Microsoft.Managed.Core.targets"),
			("VisualBasic", null, "Microsoft.VisualBasic.CurrentVersion.targets"),
			("WindowsDesktop", null, "Microsoft.NET.Sdk.WindowsDesktop.targets")
		};

		static readonly Dictionary<(string? sdkId, string filename), string> builtInSchemaMap = builtInSchemas.ToDictionary (s => (s.sdkId, s.filename), s => s.resourceId, new OrdinalIgnoreCaseTupleComparer ());

		class OrdinalIgnoreCaseTupleComparer : IEqualityComparer<(string?, string)>
		{
			public bool Equals ((string?, string) x, (string?, string) y) => string.Equals (x.Item1, y.Item1, StringComparison.OrdinalIgnoreCase) && string.Equals (x.Item2, y.Item2, StringComparison.OrdinalIgnoreCase);
			public int GetHashCode ((string?, string) obj) => HashCode.Combine (
				obj.Item1 is string item1? StringComparer.OrdinalIgnoreCase.GetHashCode (item1) : 0,
				StringComparer.OrdinalIgnoreCase.GetHashCode (obj.Item2)
			);
		}
	}

#if !NETCOREAPP2_1_OR_GREATER
	struct HashCode
	{
		public static int Combine (int a, int b) => a ^ b;
	}
#endif
}
