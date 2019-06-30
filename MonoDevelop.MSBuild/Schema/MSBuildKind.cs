// Copyright (c) 2014 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MonoDevelop.MSBuild.Schema
{
	/// <summary>
	/// Uniquely identifies elements and attributes in the MSBuild XML syntax
	/// </summary>
	/// <remarks>
	/// Any symbol without an underscore in an element, except LabelAttribute and PropertyAttribute.
	///
	/// Any symbol with an underscore in an attribute: the part before the underscore is the element name, and the part after is the attribute name.
	///
	/// An attribute name encodes the element in the bottom 16 bits: AND it with 0xFFFF to get the element.
	///
	/// The {element}_Label values are formed by ORing the element value with LabelAttribute, and similarly {element}_Condition and ConditionAttribute.
	/// This means that any condition or label attribute can be recognized by ANDing with ~0xFFFF
	/// </remarks>
	enum MSBuildKind
	{
		Unknown = 0,
		Choose = 1,
		Import = 2,
		ImportGroup = 3,
		Item = 4,
		ItemDefinitionGroup = 5,
		ItemDefinition = 6,
		ItemGroup = 7,
		Metadata = 8,
		OnError = 9,
		Otherwise = 10,
		Output = 11,
		Parameter = 12,
		ParameterGroup = 13,
		Project = 14,
		ProjectExtensions = 15,
		Property = 16,
		PropertyGroup = 17,
		Target = 18,
		Task = 19,
		TaskBody = 20,
		UsingTask = 21,
		When = 22,

		LabelAttribute = 1 << 16,
		ConditionAttribute = 2 << 16,

		Import_Label = Import | LabelAttribute,
		Import_Condition = Import | ConditionAttribute,
		Import_MinimumVersion = Import + (3 << 16),
		Import_Version = Import + (4 << 16),
		Import_Sdk = Import + (5 << 16),
		Import_Project = Import + (6 << 16),

		Item_Label = Item | LabelAttribute,
		Item_Condition = Item | ConditionAttribute,
		Item_Metadata = Item + (7 << 16),
		Item_Exclude = Item + (8 << 16),
		Item_Include = Item + (9 << 16),
		Item_Remove = Item + (10 << 16),
		Item_Update = Item + (11 << 16),
		Item_KeepMetadata = Item + (12 << 16),
		Item_RemoveMetadata = Item + (13 << 16),

		Parameter_Required = Parameter + (14 << 16),
		Parameter_Output = Parameter + (15 << 16),
		Parameter_ParameterType = Parameter + (16 << 16),

		Project_DefaultTargets = Parameter + (17 << 16),
		Project_InitialTargets = Parameter + (18 << 16),
		Project_ToolsVersion = Parameter + (19 << 16),
		Project_TreatAsLocalProperty = Parameter + (20 << 16),
		Project_xmlns = Parameter + (21 << 16),
		Project_Sdk = Parameter + (22 << 16),

		Target_Label = Target | LabelAttribute,
		Target_Condition = Target | ConditionAttribute,
		Target_Name = Target + (23 << 16),
		Target_DependsOnTargets = Target + (24 << 16),
		Target_Inputs = Target + (25 << 16),
		Target_Outputs = Target + (26 << 16),
		Target_KeepDuplicateOutputs = Target + (27 << 16),
		Target_Returns = Target + (28 << 16),
		Target_BeforeTargets = Target + (29 << 16),
		Target_AfterTargets = Target + (30 << 16),

		Property_Label = Property | LabelAttribute,
		Property_Condition = Property | ConditionAttribute,

		PropertyGroup_Label = PropertyGroup | LabelAttribute,
		PropertyGroup_Condition = PropertyGroup | ConditionAttribute,

		ImportGroup_Label = ImportGroup | LabelAttribute,
		ImportGroup_Condition = ImportGroup | ConditionAttribute,

		ItemGroup_Label = ItemGroup | LabelAttribute,
		ItemGroup_Condition = ItemGroup | ConditionAttribute,

		ItemDefinitionGroup_Label = ItemDefinitionGroup | LabelAttribute,
		ItemDefinitionGroup_Condition = ItemDefinitionGroup | ConditionAttribute,

		When_Condition = When | ConditionAttribute,

		OnError_Label = OnError | LabelAttribute,
		OnError_Condition = OnError | ConditionAttribute,
		OnError_ExecuteTargets = OnError + (31 << 16),

		UsingTask_Condition = UsingTask | ConditionAttribute,
		UsingTask_AssemblyName = UsingTask + (32 << 16),
		UsingTask_AssemblyFile = UsingTask + (33 << 16),
		UsingTask_TaskName = UsingTask + (34 << 16),
		UsingTask_TaskFactory = UsingTask + (35 << 16),
		UsingTask_Architecture = UsingTask + (36 << 16),
		UsingTask_Runtime = UsingTask + (37 << 16),
		UsingTaskBody_Evaluate = UsingTask + (38 << 16),

		Output_Condition = Output | ConditionAttribute,
		Output_TaskParameter = Output + (39 << 16),
		Output_ItemName = Output + (40 << 16),
		Output_PropertyName = Output + (41 << 16),

		Task_Condition = Output | ConditionAttribute,
		Task_Parameter = Output + (42 << 16),
		Task_ContinueOnError = Output + (43 << 16),
		Task_Architecture = Output + (44 << 16),
		Task_Runtime = Output + (45 << 16),

		Metadata_Condition = Metadata| LabelAttribute,
	}
}
