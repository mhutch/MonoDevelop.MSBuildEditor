// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MonoDevelop.MSBuild.Language;

enum MSBuildReferenceKind
{
	None,
	Item,
	Property,
	Metadata,
	Task,
	TaskParameter,
	Keyword,
	Target,
	KnownValue,
	NuGetID,
	TargetFramework,
	TargetFrameworkIdentifier,
	TargetFrameworkVersion,
	TargetFrameworkProfile,
	FileOrFolder,
	ItemFunction,
	PropertyFunction,
	StaticPropertyFunction,
	ClassName,
	Enum,
	ConditionFunction
}
