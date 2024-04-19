// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace MonoDevelop.MSBuild.Schema;

static class BuiltInSchema
{
	// don't inline this, MSBuildSchema.LoadResource gets the calling assembly
	[MethodImpl (MethodImplOptions.NoInlining)]
	public static MSBuildSchema Load (BuiltInSchemaId schemaId, out IList<MSBuildSchemaLoadError> loadErrors)
		=> MSBuildSchema.LoadResourceFromCallingAssembly ($"MonoDevelop.MSBuild.Schemas.{schemaId}.buildschema.json", out loadErrors);

	[MethodImpl (MethodImplOptions.NoInlining)]
	static MSBuildSchema Load (BuiltInSchemaId[] schemaIds, out IList<MSBuildSchemaLoadError> loadErrors)
		=> MSBuildSchema.LoadResourcesFromCallingAssembly (schemaIds.Select (schemaId => $"MonoDevelop.MSBuild.Schemas.{schemaId}.buildschema.json"), out loadErrors);

	public static MSBuildSchema? TryLoadForFile (string filePath, string sdkId, out IList<MSBuildSchemaLoadError>? loadErrors)
	{
		if (filenameToIdMap.TryGetValue (new (sdkId, Path.GetFileName (filePath)), out BuiltInSchemaId[] schemaIds)) {
			return Load (schemaIds, out loadErrors);
		}
		loadErrors = null;
		return null;
	}

	public static IEnumerable<MSBuildSchema> GetAllBuiltInFileSchemas () => filenameToIdMap.Select (s => Load (s.Value, out _));

	const string sdkTargets = "sdk.targets";

	static readonly Dictionary<(string? sdkId, string filename), BuiltInSchemaId[]> filenameToIdMap = new (BuiltInSchemaId[] resourceId, string? sdkId, string filename)[] {
		([ BuiltInSchemaId.Android ], null, "Xamarin.Android.Common.targets"),
		([ BuiltInSchemaId.Appx ], null, "Microsoft.DesktopBridge.targets"),
		([ BuiltInSchemaId.AspNetCore ], "Microsoft.NET.Sdk.Web", sdkTargets),
		([ BuiltInSchemaId.CodeAnalysis ], null, "Microsoft.CodeAnalysis.targets"),
		([ BuiltInSchemaId.CommonTargets ], null, "Microsoft.Common.targets"),
		([ BuiltInSchemaId.Cpp ], null, "Microsoft.Cpp.targets"),
		([ BuiltInSchemaId.CSharp,
		   BuiltInSchemaId.CSharpWarningCodes,
		   BuiltInSchemaId.AnalyzerWarningCodes,
		   BuiltInSchemaId.StyleRuleCodes], null, "Microsoft.CSharp.CurrentVersion.targets"),
		([ BuiltInSchemaId.ILLink ], null, "Microsoft.NET.ILLink.targets"),
		([ BuiltInSchemaId.JavaScript ], "Microsoft.VisualStudio.JavaScript.Sdk", sdkTargets),
		([ BuiltInSchemaId.NetSdk ], "Microsoft.NET.Sdk", sdkTargets),
		([ BuiltInSchemaId.NuGet ], null, "NuGet.targets"),
		([ BuiltInSchemaId.NuGetPack ], null, "NuGet.Build.Tasks.Pack.targets"),
		([ BuiltInSchemaId.GrpcProtobuf ], null, "Google.Protobuf.Tools.targets"),
		([ BuiltInSchemaId.RazorSdk ], "Microsoft.NET.Sdk.Razor", sdkTargets),
		([ BuiltInSchemaId.Roslyn ], null, "Microsoft.Managed.Core.targets"),
		([ BuiltInSchemaId.VisualBasic ], null, "Microsoft.VisualBasic.CurrentVersion.targets"),
		([ BuiltInSchemaId.WindowsDesktop ], null, "Microsoft.NET.Sdk.WindowsDesktop.targets"),
		([ BuiltInSchemaId.GenerateAssemblyInfo ], null, "Microsoft.NET.GenerateAssemblyInfo.targets"),
		([ BuiltInSchemaId.ValidatePackage ], null, "Microsoft.NET.ApiCompat.ValidatePackage.targets")
		}
		.ToDictionary (s => (s.sdkId, s.filename), s => s.resourceId, new OrdinalIgnoreCaseTupleComparer ());

	static MSBuildSchema? projectSystemMpsSchema, projectSystemCpsSchema;
	public static MSBuildSchema ProjectSystemMpsSchema => projectSystemMpsSchema ??= Load (BuiltInSchemaId.ProjectSystemMps, out _);
	public static MSBuildSchema ProjectSystemCpsSchema => projectSystemCpsSchema ??= Load (BuiltInSchemaId.ProjectSystemCps, out _);

	class OrdinalIgnoreCaseTupleComparer : IEqualityComparer<(string?, string)>
	{
		public bool Equals ((string?, string) x, (string?, string) y) => string.Equals (x.Item1, y.Item1, StringComparison.OrdinalIgnoreCase) && string.Equals (x.Item2, y.Item2, StringComparison.OrdinalIgnoreCase);
		public int GetHashCode ((string?, string) obj) => HashCode.Combine (
			obj.Item1 is string item1 ? StringComparer.OrdinalIgnoreCase.GetHashCode (item1) : 0,
			StringComparer.OrdinalIgnoreCase.GetHashCode (obj.Item2)
		);
	}

#if !NETCOREAPP2_1_OR_GREATER
	struct HashCode
	{
		public static int Combine (int a, int b) => a ^ b;
	}
#endif
}