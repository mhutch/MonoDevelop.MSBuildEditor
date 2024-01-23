// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace MonoDevelop.MSBuild.Language.Typesystem
{
	//FIXME: should there be flags for directories with/without/dontcare trailing slashes? absolute/relative paths?
	[Flags]
	public enum MSBuildValueKind
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
		Float,
		String,
		Guid,
		Url,
		Version,
		Lcid,
		Culture,
		DateTime,
		Object,
		Char,

		/// <summary>
		/// Version with a suffix, e.g. 1.0.0-beta. May be used to represent SemVer, but is less strict.
		/// </summary>
		VersionSuffixed,

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
		TaskOutputParameterName,
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
		//used by SkipNonexistentProjects property on MSBuild task
		SkipNonexistentProjectsBehavior,

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

		//we know it's an item but we don't know what kind
		UnknownItem,

		// custom type declared in a schema
		CustomType,

		// specially handled nuget types
		NuGetID,
		NuGetVersion,

		// .NET namespace and type names
		ClrNamespace,
		ClrType,
		ClrTypeName,

		// --------- MODIFIER FLAGS ---------

		/// <summary>
		/// Value that will be split into a list on semicolon separators. This is the default list kind as it is used by MSBuild for item specs.
		/// </summary>
		ListSemicolon = 1 << 28,

		/// <summary>
		/// Value that will be split into a list on comma separators. This is supported for values used by tasks that expect commas separators.
		/// </summary>
		ListComma = 1 << 29,

		/// <summary>
		/// A value that must be a literal, i.e. does not permit MSBuild expressions. Needed for certain attributes/elements read by the MSBuild engine or by Visual Studio.
		/// </summary>
		Literal = 1 << 30,

		/// <summary>
		/// Value that will be split into a list on both semicolon and comma separators. This is supported for values used by tasks that allow either separator.
		/// </summary>
		ListSemicolonOrComma = ListSemicolon | ListComma,

		/// <summary>
		/// Mask for all modifier flag bits
		/// </summary>
		AllModifiers = ListSemicolonOrComma | Literal
	}
}
