// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MonoDevelop.MSBuild.Language.Syntax;

/// <summary>
/// Constant string representing all the MSBuild element names
/// to help avoid typos and make intent clearer
/// </summary>
public static class MSBuildElementName
{
	public const string Choose = "Choose";
	public const string Import = "Import";
	public const string ImportGroup = "ImportGroup";
	public const string ItemDefinition = "ItemDefinition";
	public const string ItemDefinitionGroup = "ItemDefinitionGroup";
	public const string ItemGroup = "ItemGroup";
	public const string OnError = "OnError";
	public const string Otherwise = "Otherwise";
	public const string Output = "Output";
	public const string ParameterGroup = "ParameterGroup";
	public const string Project = "Project";
	public const string ProjectExtensions = "ProjectExtensions";
	public const string PropertyGroup = "PropertyGroup";
	public const string Sdk = "Sdk";
	public const string Target = "Target";
	public const string Task = "Task";
	public const string UsingTask = "UsingTask";
	public const string When = "When";
}
