// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace MonoDevelop.MSBuildEditor.Schema
{
	enum MSBuildValueKind
	{
		Nothing,
		Data,
		BoolLiteral,
		BoolExpression,
		TargetList,
		TaskParameterName,
		TaskParameterType,
		PropertyName,
		ItemName,
		ItemExpression,
		ConditionExpression,
		MetadataExpression,
		PropertyExpression,
		Sdk,
		SdkList,
		ProjectFilename,
		ToolsVersion,
		Xmlns,
		Label,
		TargetName,
		PropertyNameList,
		TaskAssemblyName,
		TaskAssemblyFile,
		TaskName,
		TaskFactory,
		TaskArchitecture,
		TaskRuntime,
		SdkVersion,
		TargetFrameworkVersion,
		Importance
	}
}
