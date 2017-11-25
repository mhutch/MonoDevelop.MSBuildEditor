// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

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
		MetadataName,

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
		List = 1 << 28,

		/// Allow multiple comma separated values
		CommaList = 1 << 29,

		/// Disallow expressions
		Literal = 1 << 30
	}
}
