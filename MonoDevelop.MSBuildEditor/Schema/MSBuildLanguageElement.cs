// Copyright (c) 2014 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace MonoDevelop.MSBuildEditor.Schema
{
	class MSBuildLanguageElement : ValueInfo
	{
		static readonly string[] emptyArray = new string[0];

		MSBuildLanguageElement [] children = Array.Empty<MSBuildLanguageElement> ();
		MSBuildLanguageAttribute[] attributes = Array.Empty<MSBuildLanguageAttribute> ();

		public IEnumerable<MSBuildLanguageElement> Children { get { return children; } }
		public IEnumerable<MSBuildLanguageAttribute> Attributes { get { return attributes; } }
		public MSBuildKind Kind { get; }
		public bool IsAbstract { get; }
		public MSBuildLanguageElement AbstractChild { get; private set; }
		public MSBuildLanguageAttribute AbstractAttribute { get; private set; }		

		MSBuildLanguageElement (string name, string description, MSBuildKind kind, MSBuildValueKind valueKind = MSBuildValueKind.Nothing, bool isAbstract = false)
			: base (name, description, valueKind)
		{
			Kind = kind;
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

		static MSBuildLanguageElement AddBuiltin (string name, string description, MSBuildKind kind, MSBuildValueKind valueKind = MSBuildValueKind.Nothing, bool isAbstract = false)
		{
			var el = new MSBuildLanguageElement (name, description, kind, valueKind, isAbstract);
			builtin.Add (el.Name, el);
			return el;
		}

		// this is derived from Microsoft.Build.Core.xsd
		static MSBuildLanguageElement ()
		{
			var choose = AddBuiltin ("Choose", ElementDescriptions.Choose, MSBuildKind.Choose);
			var import = AddBuiltin ("Import", ElementDescriptions.Import, MSBuildKind.Import);
			var importGroup = AddBuiltin ("ImportGroup", ElementDescriptions.ImportGroup, MSBuildKind.ImportGroup);
			var item = AddBuiltin ("Item", ElementDescriptions.Item, MSBuildKind.Item, isAbstract: true);
			var itemDefinition = AddBuiltin ("ItemDefinition", ElementDescriptions.ItemDefinition, MSBuildKind.ItemDefinition, isAbstract: true);
			var itemDefinitionGroup = AddBuiltin ("ItemDefinitionGroup", ElementDescriptions.ItemDefinitionGroup, MSBuildKind.ItemDefinitionGroup);
			var itemGroup = AddBuiltin ("ItemGroup", ElementDescriptions.ItemGroup, MSBuildKind.ItemGroup);
			var metadata = AddBuiltin ("Metadata", ElementDescriptions.Metadata, MSBuildKind.Metadata, MSBuildValueKind.Unknown, true);
			var onError = AddBuiltin ("OnError", ElementDescriptions.OnError, MSBuildKind.OnError);
			var otherwise = AddBuiltin ("Otherwise", ElementDescriptions.Otherwise, MSBuildKind.Otherwise);
			var output = AddBuiltin ("Output", ElementDescriptions.Output, MSBuildKind.Output);
			var parameter = AddBuiltin ("Parameter", ElementDescriptions.Parameter, MSBuildKind.Parameter, isAbstract: true);
			var parameterGroup = AddBuiltin ("ParameterGroup", ElementDescriptions.ParameterGroup, MSBuildKind.ParameterGroup);
			var project = AddBuiltin ("Project", ElementDescriptions.Project, MSBuildKind.Project);
			var projectExtensions = AddBuiltin ("ProjectExtensions", ElementDescriptions.ProjectExtensions, MSBuildKind.ProjectExtensions, MSBuildValueKind.Data);
			var property = AddBuiltin ("Property", ElementDescriptions.Property, MSBuildKind.Property, MSBuildValueKind.Unknown, true);
			var propertyGroup = AddBuiltin ("PropertyGroup", ElementDescriptions.PropertyGroup, MSBuildKind.PropertyGroup);
			var target = AddBuiltin ("Target", ElementDescriptions.Target, MSBuildKind.Target);
			var task = AddBuiltin ("Task", ElementDescriptions.Task, MSBuildKind.Task, isAbstract: true);
			var taskBody = AddBuiltin ("TaskBody", ElementDescriptions.TaskBody, MSBuildKind.TaskBody, MSBuildValueKind.Data);
			var usingTask = AddBuiltin ("UsingTask", ElementDescriptions.UsingTask, MSBuildKind.UsingTask);
			var when = AddBuiltin ("When", ElementDescriptions.When, MSBuildKind.When);

			choose.children = new [] { otherwise, when };
			importGroup.children = new [] { import };
			item.children = new [] { metadata };
			itemDefinition.children = new [] { metadata };
			itemDefinitionGroup.children = new [] { itemDefinition };
			itemGroup.children = new [] { item };
			otherwise.children = new [] { choose, itemGroup, propertyGroup };
			parameterGroup.children = new [] { parameter };
			project.children = new [] { choose, import, importGroup, projectExtensions, propertyGroup, itemGroup, itemDefinitionGroup, target, usingTask };
			propertyGroup.children = new [] { property };
			target.children = new [] { onError, itemGroup, propertyGroup, task };
			task.children = new [] { output };
			usingTask.children = new [] { parameterGroup, taskBody };
			when.children = new [] { choose, itemGroup, propertyGroup };

			item.AbstractChild = metadata;
			target.AbstractChild = task;
			itemDefinitionGroup.AbstractChild = itemDefinition;
			propertyGroup.AbstractChild = property;
			itemGroup.AbstractChild = item;
			parameterGroup.AbstractChild = parameter;

			import.attributes = new [] {
				new MSBuildLanguageAttribute ("Project", ElementDescriptions.Import_Project, MSBuildValueKind.ProjectFile, true),
				new MSBuildLanguageAttribute ("Condition", ElementDescriptions.Import_Condition, MSBuildValueKind.Condition),
				new MSBuildLanguageAttribute ("Label", ElementDescriptions.Import_Label, MSBuildValueKind.Label),
				new MSBuildLanguageAttribute ("Sdk", ElementDescriptions.Import_Sdk, MSBuildValueKind.Sdk),
				new MSBuildLanguageAttribute ("Version", ElementDescriptions.Import_Version, MSBuildValueKind.SdkVersion),
				new MSBuildLanguageAttribute ("MinimumVersion", ElementDescriptions.Import_MinimumVersion, MSBuildValueKind.SdkVersion),
			};

			var itemMetadataAtt = new MSBuildLanguageAttribute ("Metadata", ElementDescriptions.Metadata, MSBuildValueKind.Unknown, abstractKind: MSBuildKind.Metadata);
			item.AbstractAttribute = itemMetadataAtt;

			item.attributes = new [] {
				new MSBuildLanguageAttribute ("Exclude", ElementDescriptions.Item_Exclude, MSBuildValueKind.MatchItem),
				new MSBuildLanguageAttribute ("Include", ElementDescriptions.Item_Include, MSBuildValueKind.MatchItem),
				new MSBuildLanguageAttribute ("Remove", ElementDescriptions.Item_Remove, MSBuildValueKind.MatchItem),
				new MSBuildLanguageAttribute ("Update", ElementDescriptions.Item_Update, MSBuildValueKind.MatchItem),
				new MSBuildLanguageAttribute ("Condition", ElementDescriptions.Item_Condition, MSBuildValueKind.Condition),
				new MSBuildLanguageAttribute ("Label", ElementDescriptions.Item_Label, MSBuildValueKind.Label),
				new MSBuildLanguageAttribute ("KeepMetadata", ElementDescriptions.Item_KeepMetadata, MSBuildValueKind.MetadataName.List ()),
				new MSBuildLanguageAttribute ("RemoveMetadata", ElementDescriptions.Item_RemoveMetadata, MSBuildValueKind.MetadataName.List ()),
				new MSBuildLanguageAttribute ("KeepDuplicates", ElementDescriptions.Item_KeepDuplicates, MSBuildValueKind.Bool),
				itemMetadataAtt
			};

			parameter.attributes = new [] {
				new MSBuildLanguageAttribute ("Output", ElementDescriptions.Parameter_Output, MSBuildValueKind.Bool.Literal()),
				new MSBuildLanguageAttribute ("ParameterType", ElementDescriptions.Parameter_ParameterType, MSBuildValueKind.TaskParameterType),
				new MSBuildLanguageAttribute ("Required", ElementDescriptions.Parameter_Required, MSBuildValueKind.Bool.Literal()),
			};

			project.attributes = new [] {
				new MSBuildLanguageAttribute ("DefaultTargets", ElementDescriptions.Project_DefaultTargets, MSBuildValueKind.TargetName.List ().Literal ()),
				new MSBuildLanguageAttribute ("InitialTargets", ElementDescriptions.Project_InitialTargets, MSBuildValueKind.TargetName.List ().Literal ()),
				new MSBuildLanguageAttribute ("ToolsVersion", ElementDescriptions.Project_ToolsVersion, MSBuildValueKind.ToolsVersion.Literal ()),
				new MSBuildLanguageAttribute ("TreatAsLocalProperty", ElementDescriptions.Project_TreatAsLocalProperty, MSBuildValueKind.PropertyName.List ().Literal ()),
				new MSBuildLanguageAttribute ("xmlns", ElementDescriptions.Project_xmlns, MSBuildValueKind.Xmlns.Literal ()),
				new MSBuildLanguageAttribute ("Sdk", ElementDescriptions.Project_Sdk, MSBuildValueKind.SdkWithVersion.List().Literal ()),
			};

			target.attributes = new [] {
				new MSBuildLanguageAttribute ("Name", ElementDescriptions.Target_Name, MSBuildValueKind.TargetName.Literal (), true),
				new MSBuildLanguageAttribute ("DependsOnTargets", ElementDescriptions.Target_DependsOnTargets, MSBuildValueKind.TargetName.List()),
				new MSBuildLanguageAttribute ("Inputs", ElementDescriptions.Target_Inputs, MSBuildValueKind.Unknown),
				new MSBuildLanguageAttribute ("Outputs", ElementDescriptions.Target_Outputs, MSBuildValueKind.Unknown),
				new MSBuildLanguageAttribute ("Condition", ElementDescriptions.Target_Condition, MSBuildValueKind.Condition),
				new MSBuildLanguageAttribute ("KeepDuplicateOutputs", ElementDescriptions.Target_KeepDuplicateOutputs, MSBuildValueKind.Bool),
				new MSBuildLanguageAttribute ("Returns", ElementDescriptions.Target_Returns, MSBuildValueKind.Unknown),
				new MSBuildLanguageAttribute ("BeforeTargets", ElementDescriptions.Target_BeforeTargets, MSBuildValueKind.TargetName.List ()),
				new MSBuildLanguageAttribute ("AfterTargets", ElementDescriptions.Target_AfterTargets, MSBuildValueKind.TargetName.List ()),
				new MSBuildLanguageAttribute ("Label", ElementDescriptions.Target_Label, MSBuildValueKind.Label),
			};

			property.attributes = new [] {
				new MSBuildLanguageAttribute ("Label", ElementDescriptions.Property_Label, MSBuildValueKind.Label),
				new MSBuildLanguageAttribute ("Condition", ElementDescriptions.Property_Condition, MSBuildValueKind.Condition),
			};

			propertyGroup.attributes = new [] {
				new MSBuildLanguageAttribute ("Label", ElementDescriptions.PropertyGroup_Label, MSBuildValueKind.Label),
				new MSBuildLanguageAttribute ("Condition", ElementDescriptions.PropertyGroup_Condition, MSBuildValueKind.Condition),
			};

			importGroup.attributes = new [] {
				new MSBuildLanguageAttribute ("Label", ElementDescriptions.ImportGroup_Label, MSBuildValueKind.Label),
				new MSBuildLanguageAttribute ("Condition", ElementDescriptions.ImportGroup_Condition, MSBuildValueKind.Condition),
			};

			itemGroup.attributes = new [] {
				new MSBuildLanguageAttribute ("Label", ElementDescriptions.ItemGroup_Label, MSBuildValueKind.Label),
				new MSBuildLanguageAttribute ("Condition", ElementDescriptions.ItemGroup_Condition, MSBuildValueKind.Condition),
			};

			itemDefinitionGroup.attributes = new [] {
				new MSBuildLanguageAttribute ("Label", ElementDescriptions.ItemDefinitionGroup_Label, MSBuildValueKind.Label),
				new MSBuildLanguageAttribute ("Condition", ElementDescriptions.ItemDefinitionGroup_Condition, MSBuildValueKind.Condition),
			};

			when.attributes = new [] {
				new MSBuildLanguageAttribute ("Condition", ElementDescriptions.When_Condition, MSBuildValueKind.Condition, true),
			};

			onError.attributes = new [] {
				new MSBuildLanguageAttribute ("ExecuteTargets", ElementDescriptions.OnError_ExecuteTargets, MSBuildValueKind.TargetName.List (), true),
				new MSBuildLanguageAttribute ("Condition", ElementDescriptions.OnError_Condition, MSBuildValueKind.Condition),
				new MSBuildLanguageAttribute ("Label", ElementDescriptions.OnError_Label, MSBuildValueKind.Label),
			};

			usingTask.attributes = new [] {
				new MSBuildLanguageAttribute ("Condition", ElementDescriptions.UsingTask_Condition, MSBuildValueKind.Condition),
				new MSBuildLanguageAttribute ("AssemblyName", ElementDescriptions.UsingTask_AssemblyName, MSBuildValueKind.TaskAssemblyName),
				new MSBuildLanguageAttribute ("AssemblyFile", ElementDescriptions.UsingTask_AssemblyFile, MSBuildValueKind.TaskAssemblyFile),
				new MSBuildLanguageAttribute ("TaskName", ElementDescriptions.UsingTask_TaskName, MSBuildValueKind.TaskName, true),
				new MSBuildLanguageAttribute ("TaskFactory", ElementDescriptions.UsingTask_TaskFactory, MSBuildValueKind.TaskFactory),
				new MSBuildLanguageAttribute ("Architecture", ElementDescriptions.UsingTask_Architecture, MSBuildValueKind.TaskArchitecture),
				new MSBuildLanguageAttribute ("Runtime", ElementDescriptions.UsingTask_Runtime, MSBuildValueKind.TaskRuntime),
			};

			taskBody.attributes = new [] {
				new MSBuildLanguageAttribute ("Evaluate", ElementDescriptions.UsingTaskBody_Evaluate, MSBuildValueKind.Bool.Literal ()),
			};

			output.attributes = new [] {
				new MSBuildLanguageAttribute ("TaskParameter", ElementDescriptions.Output_TaskParameter, MSBuildValueKind.TaskParameterName, true),
				new MSBuildLanguageAttribute ("Condition", ElementDescriptions.Output, MSBuildValueKind.Condition),
				new MSBuildLanguageAttribute ("ItemName", ElementDescriptions.Output_ItemName, MSBuildValueKind.ItemName),
				new MSBuildLanguageAttribute ("PropertyName", ElementDescriptions.Output_PropertyName, MSBuildValueKind.PropertyName),
			};

			var taskParameterAtt = new MSBuildLanguageAttribute ("Parameter", ElementDescriptions.Task_Parameter, MSBuildValueKind.Unknown, abstractKind: MSBuildKind.Parameter);
			task.AbstractAttribute = taskParameterAtt;

			task.attributes = new [] {
				new MSBuildLanguageAttribute ("Condition", ElementDescriptions.Task_Condition, MSBuildValueKind.Condition),
				new MSBuildLanguageAttribute ("ContinueOnError", ElementDescriptions.Task_ContinueOnError, MSBuildValueKind.ContinueOnError),
				new MSBuildLanguageAttribute ("Architecture", ElementDescriptions.Task_Architecture, MSBuildValueKind.TaskArchitecture),
				new MSBuildLanguageAttribute ("Runtime", ElementDescriptions.Task_Runtime, MSBuildValueKind.TaskRuntime),
				taskParameterAtt
			};

			metadata.attributes = new [] {
				new MSBuildLanguageAttribute ("Condition", ElementDescriptions.Metadata_Condition, MSBuildValueKind.Condition),
			};
		}
	}
}