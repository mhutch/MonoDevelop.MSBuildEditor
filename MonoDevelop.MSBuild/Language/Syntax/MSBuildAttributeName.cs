// Copyright (c) 2014 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MonoDevelop.MSBuild.Language.Syntax;

/// <summary>
/// Constant string representing all the MSBuild attribute names
/// to help avoid typos and make intent clearer
/// </summary>
public static class MSBuildAttributeName
{
	public const string AfterTargets = "AfterTargets";
	public const string Architecture = "Architecture";
	public const string AssemblyFile = "AssemblyFile";
	public const string AssemblyName = "AssemblyName";
	public const string BeforeTargets = "BeforeTargets";
	public const string Condition = "Condition";
	public const string ContinueOnError = "ContinueOnError";
	public const string DefaultTargets = "DefaultTargets";
	public const string DependsOnTargets = "DependsOnTargets";
	public const string Evaluate = "Evaluate";
	public const string Exclude = "Exclude";
	public const string ExecuteTargets = "ExecuteTargets";
	public const string Include = "Include";
	public const string InitialTargets = "InitialTargets";
	public const string Inputs = "Inputs";
	public const string ItemName = "ItemName";
	public const string KeepDuplicateOutputs = "KeepDuplicateOutputs";
	public const string KeepDuplicates = "KeepDuplicates";
	public const string KeepMetadata = "KeepMetadata";
	public const string Label = "Label";
	public const string MinimumVersion = "MinimumVersion";
	public const string Name = "Name";
	public const string Output = "Output";
	public const string Outputs = "Outputs";
	public const string Parameter = "Parameter";
	public const string ParameterType = "ParameterType";
	public const string Project = "Project";
	public const string PropertyName = "PropertyName";
	public const string Remove = "Remove";
	public const string RemoveMetadata = "RemoveMetadata";
	public const string Required = "Required";
	public const string Returns = "Returns";
	public const string Runtime = "Runtime";
	public const string Sdk = "Sdk";
	public const string TaskFactory = "TaskFactory";
	public const string TaskName = "TaskName";
	public const string TaskParameter = "TaskParameter";
	public const string ToolsVersion = "ToolsVersion";
	public const string TreatAsLocalProperty = "TreatAsLocalProperty";
	public const string Update = "Update";
	public const string Version = "Version";
	public const string xmlns = "xmlns";
}
