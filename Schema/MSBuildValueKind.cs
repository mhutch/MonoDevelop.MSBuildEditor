// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;

namespace MonoDevelop.MSBuildEditor.Schema
{
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

		/// basic data types
		Bool,
		Int,
		String,
		Guid,
		Url,
		Version,
		SuffixedVersion,
		Lcid,

		/// references to abstract types
		TargetName,
		ItemName,
		PropertyName,
		TaskName,

		TaskAssemblyName,
		TaskAssemblyFile,
		TaskFactory,
		TaskArchitecture,
		TaskRuntime,
		TaskParameterName,
		TaskParameterType,

		Sdk,
		SdkVersion,
		SdkWithVersion,
		ToolsVersion,
		Xmlns,
		Label,
		Importance,

		Condition,

		RuntimeID,
		TargetFramework,
		TargetFrameworkIdentifier,
		TargetFrameworkVersion,
		TargetFrameworkProfile,

		ProjectFile,
		File,
		Folder,
		FileOrFolder,

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
		public static MSBuildValueKind GetDatatype (this MSBuildValueKind value)
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
