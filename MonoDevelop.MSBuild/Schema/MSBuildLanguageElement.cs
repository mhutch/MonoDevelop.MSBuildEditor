// Copyright (c) 2014 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace MonoDevelop.MSBuild.Schema
{
	class MSBuildLanguageElement : ValueInfo
	{
		static readonly string[] emptyArray = new string[0];

		MSBuildLanguageElement[] children = Array.Empty<MSBuildLanguageElement> ();
		MSBuildLanguageAttribute[] attributes = Array.Empty<MSBuildLanguageAttribute> ();

		public IEnumerable<MSBuildLanguageElement> Children { get { return children; } }
		public IEnumerable<MSBuildLanguageAttribute> Attributes { get { return attributes; } }
		public MSBuildSyntaxKind SyntaxKind { get; }
		public bool IsAbstract { get; }
		public MSBuildLanguageElement AbstractChild { get; private set; }
		public MSBuildLanguageAttribute AbstractAttribute { get; private set; }

		MSBuildLanguageElement (
			string name, DisplayText description, MSBuildSyntaxKind syntaxKind,
			MSBuildValueKind valueKind = MSBuildValueKind.Nothing,
			bool isAbstract = false, bool isDeprecated = false, string deprecationMessage = null)
			: base (name, description, valueKind, null, null, isDeprecated, deprecationMessage)
		{
			SyntaxKind = syntaxKind;
			IsAbstract = isAbstract;
		}

		public bool HasChild (string name)
		{
			return children != null && children.Any (c => string.Equals (name, c.Name, StringComparison.OrdinalIgnoreCase));
		}

		public static MSBuildLanguageElement Get (string name, MSBuildLanguageElement parent = null)
		{
			if (parent != null) {
				foreach (var child in parent.children) {
					if (string.Equals (child.Name, name, StringComparison.OrdinalIgnoreCase)) {
						return child;
					}
				}
				return parent.AbstractChild;
			}

			builtin.TryGetValue (name, out MSBuildLanguageElement result);
			return result;
		}

		public MSBuildLanguageAttribute GetAttribute (string name)
		{
			foreach (var attribute in attributes) {
				if (string.Equals (attribute.Name, name, StringComparison.OrdinalIgnoreCase)) {
					return attribute;
				}
			}
			return AbstractAttribute;
		}

		static readonly Dictionary<string, MSBuildLanguageElement> builtin = new Dictionary<string, MSBuildLanguageElement> (StringComparer.OrdinalIgnoreCase);

		static MSBuildLanguageElement AddBuiltin (string name, string description, MSBuildSyntaxKind kind, MSBuildValueKind valueKind = MSBuildValueKind.Nothing, bool isAbstract = false)
		{
			var el = new MSBuildLanguageElement (name, description, kind, valueKind, isAbstract);
			builtin.Add (el.Name, el);
			return el;
		}

		// this is derived from Microsoft.Build.Core.xsd
		static MSBuildLanguageElement ()
		{
			var choose = AddBuiltin ("Choose", ElementDescriptions.Choose, MSBuildSyntaxKind.Choose);
			var import = AddBuiltin ("Import", ElementDescriptions.Import, MSBuildSyntaxKind.Import);
			var importGroup = AddBuiltin ("ImportGroup", ElementDescriptions.ImportGroup, MSBuildSyntaxKind.ImportGroup);
			var item = AddBuiltin ("Item", ElementDescriptions.Item, MSBuildSyntaxKind.Item, isAbstract: true);
			var itemDefinition = AddBuiltin ("ItemDefinition", ElementDescriptions.ItemDefinition, MSBuildSyntaxKind.ItemDefinition, isAbstract: true);
			var itemDefinitionGroup = AddBuiltin ("ItemDefinitionGroup", ElementDescriptions.ItemDefinitionGroup, MSBuildSyntaxKind.ItemDefinitionGroup);
			var itemGroup = AddBuiltin ("ItemGroup", ElementDescriptions.ItemGroup, MSBuildSyntaxKind.ItemGroup);
			var metadata = AddBuiltin ("Metadata", ElementDescriptions.Metadata, MSBuildSyntaxKind.Metadata, MSBuildValueKind.Unknown, true);
			var onError = AddBuiltin ("OnError", ElementDescriptions.OnError, MSBuildSyntaxKind.OnError);
			var otherwise = AddBuiltin ("Otherwise", ElementDescriptions.Otherwise, MSBuildSyntaxKind.Otherwise);
			var output = AddBuiltin ("Output", ElementDescriptions.Output, MSBuildSyntaxKind.Output);
			var parameter = AddBuiltin ("Parameter", ElementDescriptions.Parameter, MSBuildSyntaxKind.Parameter, isAbstract: true);
			var parameterGroup = AddBuiltin ("ParameterGroup", ElementDescriptions.ParameterGroup, MSBuildSyntaxKind.ParameterGroup);
			var project = AddBuiltin ("Project", ElementDescriptions.Project, MSBuildSyntaxKind.Project);
			var projectExtensions = AddBuiltin ("ProjectExtensions", ElementDescriptions.ProjectExtensions, MSBuildSyntaxKind.ProjectExtensions, MSBuildValueKind.Data);
			var property = AddBuiltin ("Property", ElementDescriptions.Property, MSBuildSyntaxKind.Property, MSBuildValueKind.Unknown, true);
			var propertyGroup = AddBuiltin ("PropertyGroup", ElementDescriptions.PropertyGroup, MSBuildSyntaxKind.PropertyGroup);
			var target = AddBuiltin ("Target", ElementDescriptions.Target, MSBuildSyntaxKind.Target);
			var task = AddBuiltin ("AbstractTask", ElementDescriptions.Task, MSBuildSyntaxKind.Task, isAbstract: true);
			var taskBody = AddBuiltin ("Task", ElementDescriptions.TaskBody, MSBuildSyntaxKind.TaskBody, MSBuildValueKind.Data);
			var usingTask = AddBuiltin ("UsingTask", ElementDescriptions.UsingTask, MSBuildSyntaxKind.UsingTask);
			var when = AddBuiltin ("When", ElementDescriptions.When, MSBuildSyntaxKind.When);

			choose.children = new[] { otherwise, when };
			importGroup.children = new[] { import };
			item.children = new[] { metadata };
			itemDefinition.children = new[] { metadata };
			itemDefinitionGroup.children = new[] { itemDefinition };
			itemGroup.children = new[] { item };
			otherwise.children = new[] { choose, itemGroup, propertyGroup };
			parameterGroup.children = new[] { parameter };
			project.children = new[] { choose, import, importGroup, projectExtensions, propertyGroup, itemGroup, itemDefinitionGroup, target, usingTask };
			propertyGroup.children = new[] { property };
			target.children = new[] { onError, itemGroup, propertyGroup, task };
			task.children = new[] { output };
			usingTask.children = new[] { parameterGroup, taskBody };
			when.children = new[] { choose, itemGroup, propertyGroup };

			item.AbstractChild = metadata;
			target.AbstractChild = task;
			itemDefinitionGroup.AbstractChild = itemDefinition;
			itemDefinition.AbstractChild = metadata;
			propertyGroup.AbstractChild = property;
			itemGroup.AbstractChild = item;
			parameterGroup.AbstractChild = parameter;

			import.attributes = new[] {
				new MSBuildLanguageAttribute (import, "Project", ElementDescriptions.Import_Project, MSBuildSyntaxKind.Import_Project, MSBuildValueKind.ProjectFile, true),
				new MSBuildLanguageAttribute (import, "Condition", ElementDescriptions.Import_Condition, MSBuildSyntaxKind.Import_Condition, MSBuildValueKind.Condition),
				new MSBuildLanguageAttribute (import, "Label", ElementDescriptions.Import_Label, MSBuildSyntaxKind.Import_Label, MSBuildValueKind.Label),
				new MSBuildLanguageAttribute (import, "Sdk", ElementDescriptions.Import_Sdk, MSBuildSyntaxKind.Import_Sdk, MSBuildValueKind.Sdk),
				new MSBuildLanguageAttribute (import, "Version", ElementDescriptions.Import_Version, MSBuildSyntaxKind.Import_Version, MSBuildValueKind.SdkVersion),
				new MSBuildLanguageAttribute (import, "MinimumVersion", ElementDescriptions.Import_MinimumVersion, MSBuildSyntaxKind.Import_MinimumVersion, MSBuildValueKind.SdkVersion),
			};

			var itemMetadataAtt = new MSBuildLanguageAttribute (item, "Metadata", ElementDescriptions.Metadata, MSBuildSyntaxKind.Item_Metadata, MSBuildValueKind.Unknown, abstractKind: MSBuildSyntaxKind.Metadata);
			item.AbstractAttribute = itemMetadataAtt;

			item.attributes = new[] {
				new MSBuildLanguageAttribute (item, "Exclude", ElementDescriptions.Item_Exclude, MSBuildSyntaxKind.Item_Exclude, MSBuildValueKind.MatchItem),
				new MSBuildLanguageAttribute (item, "Include", ElementDescriptions.Item_Include, MSBuildSyntaxKind.Item_Include, MSBuildValueKind.MatchItem),
				new MSBuildLanguageAttribute (item, "Remove", ElementDescriptions.Item_Remove, MSBuildSyntaxKind.Item_Remove, MSBuildValueKind.MatchItem),
				new MSBuildLanguageAttribute (item, "Update", ElementDescriptions.Item_Update, MSBuildSyntaxKind.Item_Update, MSBuildValueKind.MatchItem),
				new MSBuildLanguageAttribute (item, "Condition", ElementDescriptions.Item_Condition, MSBuildSyntaxKind.Item_Condition, MSBuildValueKind.Condition),
				new MSBuildLanguageAttribute (item, "Label", ElementDescriptions.Item_Label, MSBuildSyntaxKind.Item_Label, MSBuildValueKind.Label),
				new MSBuildLanguageAttribute (item, "KeepMetadata", ElementDescriptions.Item_KeepMetadata, MSBuildSyntaxKind.Item_KeepMetadata, MSBuildValueKind.MetadataName.List ()),
				new MSBuildLanguageAttribute (item, "RemoveMetadata", ElementDescriptions.Item_RemoveMetadata, MSBuildSyntaxKind.Item_RemoveMetadata, MSBuildValueKind.MetadataName.List ()),
				new MSBuildLanguageAttribute (item, "KeepDuplicates", ElementDescriptions.Item_KeepDuplicates, MSBuildSyntaxKind.Parameter_Required, MSBuildValueKind.Bool),
				itemMetadataAtt
			};

			parameter.attributes = new[] {
				new MSBuildLanguageAttribute (parameter, "Output", ElementDescriptions.Parameter_Output, MSBuildSyntaxKind.Parameter_Output, MSBuildValueKind.Bool.Literal()),
				new MSBuildLanguageAttribute (parameter, "ParameterType", ElementDescriptions.Parameter_ParameterType, MSBuildSyntaxKind.Parameter_ParameterType, MSBuildValueKind.TaskParameterType),
				new MSBuildLanguageAttribute (parameter, "Required", ElementDescriptions.Parameter_Required, MSBuildSyntaxKind.Parameter_Required, MSBuildValueKind.Bool.Literal()),
			};

			project.attributes = new[] {
				new MSBuildLanguageAttribute (project, "DefaultTargets", ElementDescriptions.Project_DefaultTargets, MSBuildSyntaxKind.Project_DefaultTargets, MSBuildValueKind.TargetName.List ().Literal ()),
				new MSBuildLanguageAttribute (project, "InitialTargets", ElementDescriptions.Project_InitialTargets, MSBuildSyntaxKind.Project_InitialTargets, MSBuildValueKind.TargetName.List ().Literal ()),
				new MSBuildLanguageAttribute (project, "ToolsVersion", ElementDescriptions.Project_ToolsVersion, MSBuildSyntaxKind.Project_ToolsVersion, MSBuildValueKind.ToolsVersion.Literal (), isDeprecated: true),
				new MSBuildLanguageAttribute (project, "TreatAsLocalProperty", ElementDescriptions.Project_TreatAsLocalProperty, MSBuildSyntaxKind.Project_TreatAsLocalProperty, MSBuildValueKind.PropertyName.List ().Literal ()),
				new MSBuildLanguageAttribute (project, "xmlns", ElementDescriptions.Project_xmlns, MSBuildSyntaxKind.Project_xmlns, MSBuildValueKind.Xmlns.Literal ()),
				new MSBuildLanguageAttribute (project, "Sdk", ElementDescriptions.Project_Sdk, MSBuildSyntaxKind.Project_Sdk, MSBuildValueKind.SdkWithVersion.List().Literal ()),
			};

			target.attributes = new[] {
				new MSBuildLanguageAttribute (target, "Name", ElementDescriptions.Target_Name, MSBuildSyntaxKind.Target_Name, MSBuildValueKind.TargetName.Literal (), true),
				new MSBuildLanguageAttribute (target, "DependsOnTargets", ElementDescriptions.Target_DependsOnTargets, MSBuildSyntaxKind.Target_DependsOnTargets, MSBuildValueKind.TargetName.List()),
				new MSBuildLanguageAttribute (target, "Inputs", ElementDescriptions.Target_Inputs, MSBuildSyntaxKind.Target_Inputs, MSBuildValueKind.Unknown),
				new MSBuildLanguageAttribute (target, "Outputs", ElementDescriptions.Target_Outputs, MSBuildSyntaxKind.Target_Outputs, MSBuildValueKind.Unknown),
				new MSBuildLanguageAttribute (target, "Condition", ElementDescriptions.Target_Condition, MSBuildSyntaxKind.Target_Condition, MSBuildValueKind.Condition),
				new MSBuildLanguageAttribute (target, "KeepDuplicateOutputs", ElementDescriptions.Target_KeepDuplicateOutputs, MSBuildSyntaxKind.Target_KeepDuplicateOutputs, MSBuildValueKind.Bool),
				new MSBuildLanguageAttribute (target, "Returns", ElementDescriptions.Target_Returns, MSBuildSyntaxKind.Target_Returns, MSBuildValueKind.Unknown),
				new MSBuildLanguageAttribute (target, "BeforeTargets", ElementDescriptions.Target_BeforeTargets, MSBuildSyntaxKind.Target_BeforeTargets, MSBuildValueKind.TargetName.List ()),
				new MSBuildLanguageAttribute (target, "AfterTargets", ElementDescriptions.Target_AfterTargets, MSBuildSyntaxKind.Target_AfterTargets, MSBuildValueKind.TargetName.List ()),
				new MSBuildLanguageAttribute (target, "Label", ElementDescriptions.Target_Label, MSBuildSyntaxKind.Target_Label, MSBuildValueKind.Label),
			};

			property.attributes = new[] {
				new MSBuildLanguageAttribute (property, "Label", ElementDescriptions.Property_Label, MSBuildSyntaxKind.Property_Label, MSBuildValueKind.Label),
				new MSBuildLanguageAttribute (property, "Condition", ElementDescriptions.Property_Condition, MSBuildSyntaxKind.Property_Condition, MSBuildValueKind.Condition),
			};

			propertyGroup.attributes = new[] {
				new MSBuildLanguageAttribute (propertyGroup, "Label", ElementDescriptions.PropertyGroup_Label, MSBuildSyntaxKind.PropertyGroup_Label, MSBuildValueKind.Label),
				new MSBuildLanguageAttribute (propertyGroup, "Condition", ElementDescriptions.PropertyGroup_Condition, MSBuildSyntaxKind.PropertyGroup_Condition, MSBuildValueKind.Condition),
			};

			importGroup.attributes = new[] {
				new MSBuildLanguageAttribute (importGroup, "Label", ElementDescriptions.ImportGroup_Label, MSBuildSyntaxKind.ImportGroup_Label, MSBuildValueKind.Label),
				new MSBuildLanguageAttribute (importGroup, "Condition", ElementDescriptions.ImportGroup_Condition, MSBuildSyntaxKind.ImportGroup_Condition, MSBuildValueKind.Condition),
			};

			itemGroup.attributes = new[] {
				new MSBuildLanguageAttribute (itemGroup, "Label", ElementDescriptions.ItemGroup_Label, MSBuildSyntaxKind.ItemGroup_Label, MSBuildValueKind.Label),
				new MSBuildLanguageAttribute (itemGroup, "Condition", ElementDescriptions.ItemGroup_Condition, MSBuildSyntaxKind.ItemGroup_Condition, MSBuildValueKind.Condition),
			};

			itemDefinitionGroup.attributes = new[] {
				new MSBuildLanguageAttribute (itemDefinitionGroup, "Label", ElementDescriptions.ItemDefinitionGroup_Label, MSBuildSyntaxKind.ItemDefinitionGroup_Label, MSBuildValueKind.Label),
				new MSBuildLanguageAttribute (itemDefinitionGroup, "Condition", ElementDescriptions.ItemDefinitionGroup_Condition, MSBuildSyntaxKind.ItemDefinitionGroup_Condition, MSBuildValueKind.Condition),
			};

			when.attributes = new[] {
				new MSBuildLanguageAttribute (when, "Condition", ElementDescriptions.When_Condition, MSBuildSyntaxKind.When_Condition, MSBuildValueKind.Condition, true),
			};

			onError.attributes = new[] {
				new MSBuildLanguageAttribute (onError, "ExecuteTargets", ElementDescriptions.OnError_ExecuteTargets, MSBuildSyntaxKind.OnError_ExecuteTargets, MSBuildValueKind.TargetName.List (), true),
				new MSBuildLanguageAttribute (onError, "Condition", ElementDescriptions.OnError_Condition, MSBuildSyntaxKind.OnError_Condition, MSBuildValueKind.Condition),
				new MSBuildLanguageAttribute (onError, "Label", ElementDescriptions.OnError_Label, MSBuildSyntaxKind.OnError_Label, MSBuildValueKind.Label),
			};

			usingTask.attributes = new[] {
				new MSBuildLanguageAttribute (usingTask, "Condition", ElementDescriptions.UsingTask_Condition, MSBuildSyntaxKind.UsingTask_Condition, MSBuildValueKind.Condition),
				new MSBuildLanguageAttribute (usingTask, "AssemblyName", ElementDescriptions.UsingTask_AssemblyName, MSBuildSyntaxKind.UsingTask_AssemblyName, MSBuildValueKind.TaskAssemblyName),
				new MSBuildLanguageAttribute (usingTask, "AssemblyFile", ElementDescriptions.UsingTask_AssemblyFile, MSBuildSyntaxKind.UsingTask_AssemblyFile, MSBuildValueKind.TaskAssemblyFile),
				new MSBuildLanguageAttribute (usingTask, "TaskName", ElementDescriptions.UsingTask_TaskName, MSBuildSyntaxKind.UsingTask_TaskName, MSBuildValueKind.TaskName, true),
				new MSBuildLanguageAttribute (usingTask, "TaskFactory", ElementDescriptions.UsingTask_TaskFactory, MSBuildSyntaxKind.UsingTask_TaskFactory, MSBuildValueKind.TaskFactory),
				new MSBuildLanguageAttribute (usingTask, "Architecture", ElementDescriptions.UsingTask_Architecture, MSBuildSyntaxKind.UsingTask_Architecture, MSBuildValueKind.TaskArchitecture),
				new MSBuildLanguageAttribute (usingTask, "Runtime", ElementDescriptions.UsingTask_Runtime, MSBuildSyntaxKind.UsingTask_Runtime, MSBuildValueKind.TaskRuntime),
			};

			taskBody.attributes = new[] {
				new MSBuildLanguageAttribute (taskBody, "Evaluate", ElementDescriptions.UsingTaskBody_Evaluate, MSBuildSyntaxKind.UsingTaskBody_Evaluate, MSBuildValueKind.Bool.Literal ()),
			};

			output.attributes = new[] {
				new MSBuildLanguageAttribute (output, "TaskParameter", ElementDescriptions.Output_TaskParameter, MSBuildSyntaxKind.Output_TaskParameter, MSBuildValueKind.TaskOutputParameterName.Literal (), true),
				new MSBuildLanguageAttribute (output, "Condition", ElementDescriptions.Output_Condition, MSBuildSyntaxKind.Output_Condition,  MSBuildValueKind.Condition),
				new MSBuildLanguageAttribute (output, "ItemName", ElementDescriptions.Output_ItemName, MSBuildSyntaxKind.Output_ItemName, MSBuildValueKind.ItemName.Literal ()),
				new MSBuildLanguageAttribute (output, "PropertyName", ElementDescriptions.Output_PropertyName, MSBuildSyntaxKind.Output_PropertyName, MSBuildValueKind.PropertyName.Literal ()),
			};

			var taskParameterAtt = new MSBuildLanguageAttribute (task, "Parameter", ElementDescriptions.Task_Parameter, MSBuildSyntaxKind.Task_Parameter, MSBuildValueKind.Unknown, abstractKind: MSBuildSyntaxKind.Parameter);
			task.AbstractAttribute = taskParameterAtt;

			task.attributes = new[] {
				new MSBuildLanguageAttribute (task, "Condition", ElementDescriptions.Task_Condition, MSBuildSyntaxKind.Task_Condition, MSBuildValueKind.Condition),
				new MSBuildLanguageAttribute (task, "ContinueOnError", ElementDescriptions.Task_ContinueOnError, MSBuildSyntaxKind.Task_ContinueOnError, MSBuildValueKind.ContinueOnError),
				new MSBuildLanguageAttribute (task, "Architecture", ElementDescriptions.Task_Architecture, MSBuildSyntaxKind.Task_Architecture, MSBuildValueKind.TaskArchitecture),
				new MSBuildLanguageAttribute (task, "Runtime", ElementDescriptions.Task_Runtime, MSBuildSyntaxKind.Task_Runtime, MSBuildValueKind.TaskRuntime),
				taskParameterAtt
			};

			metadata.attributes = new[] {
				new MSBuildLanguageAttribute (metadata, "Condition", ElementDescriptions.Metadata_Condition, MSBuildSyntaxKind.Metadata_Condition, MSBuildValueKind.Condition),
			};
		}
	}
}