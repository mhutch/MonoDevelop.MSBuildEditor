// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;

namespace MonoDevelop.MSBuildEditor.Schema
{
	//FIXME: should there be flags for directories with/without/dontcare trailing slashes? absolute/relative paths?
	[Flags]
	enum MSBuildValueKind
	{
		Unknown,

		//used to flag attributes where the value type
		//matches the item value type, such as Include
		MatchItem,

		/// Must be empty
		Nothing,

		/// Contains arbitrary data
		Data,

		// basic data types
		Bool,
		Int,
		String,
		Guid,
		Url,
		Version,
		SuffixedVersion,
		Lcid,
		DateTime,

		// references to abstract types
		TargetName,
		ItemName,
		PropertyName,

		//task stuff
		TaskName,
		TaskAssemblyName,
		TaskAssemblyFile,
		TaskFactory,
		TaskArchitecture,
		TaskRuntime,
		TaskParameterName,
		TaskParameterType,

		//things related to SDKs
		Sdk,
		SdkVersion,
		SdkWithVersion,

		//fundamental builtin stuff
		ToolsVersion,
		Xmlns,
		Label,
		Importance,
		HostOS,
		HostRuntime,
		ContinueOnError,

		Condition,

		Configuration,
		Platform,

		RuntimeID,
		TargetFramework,
		TargetFrameworkIdentifier,
		TargetFrameworkVersion,
		TargetFrameworkProfile,
		TargetFrameworkMoniker,

		ProjectFile,
		File,
		Folder,
		FolderWithSlash,
		FileOrFolder,
		Extension,
		Filename,

		NuGetID,
		NuGetVersion,

		/// Allow multiple semicolon separated values
		List = 1 << 29,

		/// Disallow expressions
		Literal = 1 << 30
	}

	static class ValueKindExtensions
	{
		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		public static MSBuildValueKind GetScalarType (this MSBuildValueKind value)
		{
			return value & ~(MSBuildValueKind.List | MSBuildValueKind.Literal);
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		public static bool AllowExpressions (this MSBuildValueKind value)
		{
			return (value & MSBuildValueKind.Literal) == 0;
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		public static bool AllowLists (this MSBuildValueKind value)
		{
			return (value & MSBuildValueKind.List) != 0 || value == MSBuildValueKind.Unknown;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static MSBuildValueKind List (this MSBuildValueKind value)
		{
			return value | MSBuildValueKind.List;
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		public static MSBuildValueKind Literal (this MSBuildValueKind value)
		{
			return value | MSBuildValueKind.Literal;
		}
	}
}
