// Copyright (c) 2014 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Schema
{
	public class MSBuildElementSyntax : ValueInfo
	{
		static readonly string[] emptyArray = new string[0];

		MSBuildElementSyntax[] children = Array.Empty<MSBuildElementSyntax> ();
		MSBuildAttributeSyntax[] attributes = Array.Empty<MSBuildAttributeSyntax> ();

		public IEnumerable<MSBuildElementSyntax> Children { get { return children; } }
		public IEnumerable<MSBuildAttributeSyntax> Attributes { get { return attributes; } }
		public MSBuildSyntaxKind SyntaxKind { get; }
		public bool IsAbstract { get; }
		public MSBuildElementSyntax AbstractChild { get; private set; }
		public MSBuildAttributeSyntax AbstractAttribute { get; private set; }

		MSBuildElementSyntax (
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

		public static MSBuildElementSyntax Get (string name, MSBuildElementSyntax parent = null)
		{
			if (parent != null) {
				foreach (var child in parent.children) {
					if (string.Equals (child.Name, name, StringComparison.OrdinalIgnoreCase)) {
						return child;
					}
				}
				return parent.AbstractChild;
			}

			builtin.TryGetValue (name, out MSBuildElementSyntax result);
			return result;
		}

		public static (MSBuildElementSyntax element, MSBuildAttributeSyntax attribute)? Get (IEnumerable<XObject> path)
		{
			MSBuildElementSyntax elementSyntax = null;
			foreach (var n in path) {
				if (elementSyntax == null) {
					if (n is XDocument) {
						continue;
					}
					if (n is XElement xroot && xroot.NameEquals ("Project", true)) {
						elementSyntax = Project;
						continue;
					}
					return null;
				}
				if (n is XElement xel) {
					elementSyntax = Get (xel.Name.FullName, elementSyntax);
					if (elementSyntax != null) {
						continue;
					}
					return null;
				}
				if (n is XAttribute att) {
					var attributeSyntax = elementSyntax.GetAttribute (att.Name.FullName);
					if (attributeSyntax != null) {
						return (elementSyntax, attributeSyntax);
					}
					return null;
				}
				if (n is XText) {
					return (elementSyntax, null);
				}
			}
			return null;
		}

		public MSBuildAttributeSyntax GetAttribute (string name)
		{
			foreach (var attribute in attributes) {
				if (string.Equals (attribute.Name, name, StringComparison.OrdinalIgnoreCase)) {
					return attribute;
				}
			}
			return AbstractAttribute;
		}

		static readonly Dictionary<string, MSBuildElementSyntax> builtin = new Dictionary<string, MSBuildElementSyntax> (StringComparer.OrdinalIgnoreCase);

		static MSBuildElementSyntax AddBuiltin (string name, string description, MSBuildSyntaxKind kind, MSBuildValueKind valueKind = MSBuildValueKind.Nothing, bool isAbstract = false)
		{
			var el = new MSBuildElementSyntax (name, description, kind, valueKind, isAbstract);
			builtin.Add (el.Name, el);
			return el;
		}

		public static MSBuildElementSyntax Choose { get; }
		public static MSBuildElementSyntax Import { get; }
		public static MSBuildElementSyntax ImportGroup { get; }
		public static MSBuildElementSyntax Item { get; }
		public static MSBuildElementSyntax ItemDefinition { get; }
		public static MSBuildElementSyntax ItemDefinitionGroup { get; }
		public static MSBuildElementSyntax ItemGroup { get; }
		public static MSBuildElementSyntax Metadata { get; }
		public static MSBuildElementSyntax OnError { get; }
		public static MSBuildElementSyntax Otherwise { get; }
		public static MSBuildElementSyntax Output { get; }
		public static MSBuildElementSyntax Parameter { get; }
		public static MSBuildElementSyntax ParameterGroup { get; }
		public static MSBuildElementSyntax Project { get; }
		public static MSBuildElementSyntax ProjectExtensions { get; }
		public static MSBuildElementSyntax Property { get; }
		public static MSBuildElementSyntax PropertyGroup { get; }
		public static MSBuildElementSyntax Target { get; }
		public static MSBuildElementSyntax Task { get; }
		public static MSBuildElementSyntax TaskBody { get; }
		public static MSBuildElementSyntax UsingTask { get; }
		public static MSBuildElementSyntax When { get; }

		// this is derived from Microsoft.Build.Core.xsd
		static MSBuildElementSyntax ()
		{
			Choose = AddBuiltin ("Choose", ElementDescriptions.Choose, MSBuildSyntaxKind.Choose);
			Import = AddBuiltin ("Import", ElementDescriptions.Import, MSBuildSyntaxKind.Import);
			ImportGroup = AddBuiltin ("ImportGroup", ElementDescriptions.ImportGroup, MSBuildSyntaxKind.ImportGroup);
			Item = AddBuiltin ("Item", ElementDescriptions.Item, MSBuildSyntaxKind.Item, isAbstract: true);
			ItemDefinition = AddBuiltin ("ItemDefinition", ElementDescriptions.ItemDefinition, MSBuildSyntaxKind.ItemDefinition, isAbstract: true);
			ItemDefinitionGroup = AddBuiltin ("ItemDefinitionGroup", ElementDescriptions.ItemDefinitionGroup, MSBuildSyntaxKind.ItemDefinitionGroup);
			ItemGroup = AddBuiltin ("ItemGroup", ElementDescriptions.ItemGroup, MSBuildSyntaxKind.ItemGroup);
			Metadata = AddBuiltin ("Metadata", ElementDescriptions.Metadata, MSBuildSyntaxKind.Metadata, MSBuildValueKind.Unknown, true);
			OnError = AddBuiltin ("OnError", ElementDescriptions.OnError, MSBuildSyntaxKind.OnError);
			Otherwise = AddBuiltin ("Otherwise", ElementDescriptions.Otherwise, MSBuildSyntaxKind.Otherwise);
			Output = AddBuiltin ("Output", ElementDescriptions.Output, MSBuildSyntaxKind.Output);
			Parameter = AddBuiltin ("Parameter", ElementDescriptions.Parameter, MSBuildSyntaxKind.Parameter, isAbstract: true);
			ParameterGroup = AddBuiltin ("ParameterGroup", ElementDescriptions.ParameterGroup, MSBuildSyntaxKind.ParameterGroup);
			Project = AddBuiltin ("Project", ElementDescriptions.Project, MSBuildSyntaxKind.Project);
			ProjectExtensions = AddBuiltin ("ProjectExtensions", ElementDescriptions.ProjectExtensions, MSBuildSyntaxKind.ProjectExtensions, MSBuildValueKind.Data);
			Property = AddBuiltin ("Property", ElementDescriptions.Property, MSBuildSyntaxKind.Property, MSBuildValueKind.Unknown, true);
			PropertyGroup = AddBuiltin ("PropertyGroup", ElementDescriptions.PropertyGroup, MSBuildSyntaxKind.PropertyGroup);
			Target = AddBuiltin ("Target", ElementDescriptions.Target, MSBuildSyntaxKind.Target);
			Task = AddBuiltin ("AbstractTask", ElementDescriptions.Task, MSBuildSyntaxKind.Task, isAbstract: true);
			TaskBody = AddBuiltin ("Task", ElementDescriptions.TaskBody, MSBuildSyntaxKind.TaskBody, MSBuildValueKind.Data);
			UsingTask = AddBuiltin ("UsingTask", ElementDescriptions.UsingTask, MSBuildSyntaxKind.UsingTask);
			When = AddBuiltin ("When", ElementDescriptions.When, MSBuildSyntaxKind.When);

			Choose.children = new[] { Otherwise, When };
			ImportGroup.children = new[] { Import };
			Item.children = new[] { Metadata };
			ItemDefinition.children = new[] { Metadata };
			ItemDefinitionGroup.children = new[] { ItemDefinition };
			ItemGroup.children = new[] { Item };
			Otherwise.children = new[] { Choose, ItemGroup, PropertyGroup };
			ParameterGroup.children = new[] { Parameter };
			Project.children = new[] { Choose, Import, ImportGroup, ProjectExtensions, PropertyGroup, ItemGroup, ItemDefinitionGroup, Target, UsingTask };
			PropertyGroup.children = new[] { Property };
			Target.children = new[] { OnError, ItemGroup, PropertyGroup, Task };
			Task.children = new[] { Output };
			UsingTask.children = new[] { ParameterGroup, TaskBody };
			When.children = new[] { Choose, ItemGroup, PropertyGroup };

			Item.AbstractChild = Metadata;
			Target.AbstractChild = Task;
			ItemDefinitionGroup.AbstractChild = ItemDefinition;
			ItemDefinition.AbstractChild = Metadata;
			PropertyGroup.AbstractChild = Property;
			ItemGroup.AbstractChild = Item;
			ParameterGroup.AbstractChild = Parameter;

			Import.attributes = new[] {
				new MSBuildAttributeSyntax (Import, "Project", ElementDescriptions.Import_Project, MSBuildSyntaxKind.Import_Project, MSBuildValueKind.ProjectFile, true),
				new MSBuildAttributeSyntax (Import, "Condition", ElementDescriptions.Import_Condition, MSBuildSyntaxKind.Import_Condition, MSBuildValueKind.Condition),
				new MSBuildAttributeSyntax (Import, "Label", ElementDescriptions.Import_Label, MSBuildSyntaxKind.Import_Label, MSBuildValueKind.Label),
				new MSBuildAttributeSyntax (Import, "Sdk", ElementDescriptions.Import_Sdk, MSBuildSyntaxKind.Import_Sdk, MSBuildValueKind.Sdk),
				new MSBuildAttributeSyntax (Import, "Version", ElementDescriptions.Import_Version, MSBuildSyntaxKind.Import_Version, MSBuildValueKind.SdkVersion),
				new MSBuildAttributeSyntax (Import, "MinimumVersion", ElementDescriptions.Import_MinimumVersion, MSBuildSyntaxKind.Import_MinimumVersion, MSBuildValueKind.SdkVersion),
			};

			var itemMetadataAtt = new MSBuildAttributeSyntax (Item, "Metadata", ElementDescriptions.Metadata, MSBuildSyntaxKind.Item_Metadata, MSBuildValueKind.Unknown, abstractKind: MSBuildSyntaxKind.Metadata);
			Item.AbstractAttribute = itemMetadataAtt;

			Item.attributes = new[] {
				new MSBuildAttributeSyntax (Item, "Exclude", ElementDescriptions.Item_Exclude, MSBuildSyntaxKind.Item_Exclude, MSBuildValueKind.MatchItem),
				new MSBuildAttributeSyntax (Item, "Include", ElementDescriptions.Item_Include, MSBuildSyntaxKind.Item_Include, MSBuildValueKind.MatchItem),
				new MSBuildAttributeSyntax (Item, "Remove", ElementDescriptions.Item_Remove, MSBuildSyntaxKind.Item_Remove, MSBuildValueKind.MatchItem),
				new MSBuildAttributeSyntax (Item, "Update", ElementDescriptions.Item_Update, MSBuildSyntaxKind.Item_Update, MSBuildValueKind.MatchItem),
				new MSBuildAttributeSyntax (Item, "Condition", ElementDescriptions.Item_Condition, MSBuildSyntaxKind.Item_Condition, MSBuildValueKind.Condition),
				new MSBuildAttributeSyntax (Item, "Label", ElementDescriptions.Item_Label, MSBuildSyntaxKind.Item_Label, MSBuildValueKind.Label),
				new MSBuildAttributeSyntax (Item, "KeepMetadata", ElementDescriptions.Item_KeepMetadata, MSBuildSyntaxKind.Item_KeepMetadata, MSBuildValueKind.MetadataName.List ()),
				new MSBuildAttributeSyntax (Item, "RemoveMetadata", ElementDescriptions.Item_RemoveMetadata, MSBuildSyntaxKind.Item_RemoveMetadata, MSBuildValueKind.MetadataName.List ()),
				new MSBuildAttributeSyntax (Item, "KeepDuplicates", ElementDescriptions.Item_KeepDuplicates, MSBuildSyntaxKind.Parameter_Required, MSBuildValueKind.Bool),
				itemMetadataAtt
			};

			Parameter.attributes = new[] {
				new MSBuildAttributeSyntax (Parameter, "Output", ElementDescriptions.Parameter_Output, MSBuildSyntaxKind.Parameter_Output, MSBuildValueKind.Bool.Literal()),
				new MSBuildAttributeSyntax (Parameter, "ParameterType", ElementDescriptions.Parameter_ParameterType, MSBuildSyntaxKind.Parameter_ParameterType, MSBuildValueKind.TaskParameterType),
				new MSBuildAttributeSyntax (Parameter, "Required", ElementDescriptions.Parameter_Required, MSBuildSyntaxKind.Parameter_Required, MSBuildValueKind.Bool.Literal()),
			};

			Project.attributes = new[] {
				new MSBuildAttributeSyntax (Project, "DefaultTargets", ElementDescriptions.Project_DefaultTargets, MSBuildSyntaxKind.Project_DefaultTargets, MSBuildValueKind.TargetName.List ().Literal ()),
				new MSBuildAttributeSyntax (Project, "InitialTargets", ElementDescriptions.Project_InitialTargets, MSBuildSyntaxKind.Project_InitialTargets, MSBuildValueKind.TargetName.List ().Literal ()),
				new MSBuildAttributeSyntax (Project, "ToolsVersion", ElementDescriptions.Project_ToolsVersion, MSBuildSyntaxKind.Project_ToolsVersion, MSBuildValueKind.ToolsVersion.Literal (), isDeprecated: true),
				new MSBuildAttributeSyntax (Project, "TreatAsLocalProperty", ElementDescriptions.Project_TreatAsLocalProperty, MSBuildSyntaxKind.Project_TreatAsLocalProperty, MSBuildValueKind.PropertyName.List ().Literal ()),
				new MSBuildAttributeSyntax (Project, "xmlns", ElementDescriptions.Project_xmlns, MSBuildSyntaxKind.Project_xmlns, MSBuildValueKind.Xmlns.Literal ()),
				new MSBuildAttributeSyntax (Project, "Sdk", ElementDescriptions.Project_Sdk, MSBuildSyntaxKind.Project_Sdk, MSBuildValueKind.SdkWithVersion.List().Literal ()),
			};

			Target.attributes = new[] {
				new MSBuildAttributeSyntax (Target, "Name", ElementDescriptions.Target_Name, MSBuildSyntaxKind.Target_Name, MSBuildValueKind.TargetName.Literal (), true),
				new MSBuildAttributeSyntax (Target, "DependsOnTargets", ElementDescriptions.Target_DependsOnTargets, MSBuildSyntaxKind.Target_DependsOnTargets, MSBuildValueKind.TargetName.List()),
				new MSBuildAttributeSyntax (Target, "Inputs", ElementDescriptions.Target_Inputs, MSBuildSyntaxKind.Target_Inputs, MSBuildValueKind.Unknown),
				new MSBuildAttributeSyntax (Target, "Outputs", ElementDescriptions.Target_Outputs, MSBuildSyntaxKind.Target_Outputs, MSBuildValueKind.Unknown),
				new MSBuildAttributeSyntax (Target, "Condition", ElementDescriptions.Target_Condition, MSBuildSyntaxKind.Target_Condition, MSBuildValueKind.Condition),
				new MSBuildAttributeSyntax (Target, "KeepDuplicateOutputs", ElementDescriptions.Target_KeepDuplicateOutputs, MSBuildSyntaxKind.Target_KeepDuplicateOutputs, MSBuildValueKind.Bool),
				new MSBuildAttributeSyntax (Target, "Returns", ElementDescriptions.Target_Returns, MSBuildSyntaxKind.Target_Returns, MSBuildValueKind.Unknown),
				new MSBuildAttributeSyntax (Target, "BeforeTargets", ElementDescriptions.Target_BeforeTargets, MSBuildSyntaxKind.Target_BeforeTargets, MSBuildValueKind.TargetName.List ()),
				new MSBuildAttributeSyntax (Target, "AfterTargets", ElementDescriptions.Target_AfterTargets, MSBuildSyntaxKind.Target_AfterTargets, MSBuildValueKind.TargetName.List ()),
				new MSBuildAttributeSyntax (Target, "Label", ElementDescriptions.Target_Label, MSBuildSyntaxKind.Target_Label, MSBuildValueKind.Label),
			};

			Property.attributes = new[] {
				new MSBuildAttributeSyntax (Property, "Label", ElementDescriptions.Property_Label, MSBuildSyntaxKind.Property_Label, MSBuildValueKind.Label),
				new MSBuildAttributeSyntax (Property, "Condition", ElementDescriptions.Property_Condition, MSBuildSyntaxKind.Property_Condition, MSBuildValueKind.Condition),
			};

			PropertyGroup.attributes = new[] {
				new MSBuildAttributeSyntax (PropertyGroup, "Label", ElementDescriptions.PropertyGroup_Label, MSBuildSyntaxKind.PropertyGroup_Label, MSBuildValueKind.Label),
				new MSBuildAttributeSyntax (PropertyGroup, "Condition", ElementDescriptions.PropertyGroup_Condition, MSBuildSyntaxKind.PropertyGroup_Condition, MSBuildValueKind.Condition),
			};

			ImportGroup.attributes = new[] {
				new MSBuildAttributeSyntax (ImportGroup, "Label", ElementDescriptions.ImportGroup_Label, MSBuildSyntaxKind.ImportGroup_Label, MSBuildValueKind.Label),
				new MSBuildAttributeSyntax (ImportGroup, "Condition", ElementDescriptions.ImportGroup_Condition, MSBuildSyntaxKind.ImportGroup_Condition, MSBuildValueKind.Condition),
			};

			ItemGroup.attributes = new[] {
				new MSBuildAttributeSyntax (ItemGroup, "Label", ElementDescriptions.ItemGroup_Label, MSBuildSyntaxKind.ItemGroup_Label, MSBuildValueKind.Label),
				new MSBuildAttributeSyntax (ItemGroup, "Condition", ElementDescriptions.ItemGroup_Condition, MSBuildSyntaxKind.ItemGroup_Condition, MSBuildValueKind.Condition),
			};

			ItemDefinitionGroup.attributes = new[] {
				new MSBuildAttributeSyntax (ItemDefinitionGroup, "Label", ElementDescriptions.ItemDefinitionGroup_Label, MSBuildSyntaxKind.ItemDefinitionGroup_Label, MSBuildValueKind.Label),
				new MSBuildAttributeSyntax (ItemDefinitionGroup, "Condition", ElementDescriptions.ItemDefinitionGroup_Condition, MSBuildSyntaxKind.ItemDefinitionGroup_Condition, MSBuildValueKind.Condition),
			};

			When.attributes = new[] {
				new MSBuildAttributeSyntax (When, "Condition", ElementDescriptions.When_Condition, MSBuildSyntaxKind.When_Condition, MSBuildValueKind.Condition, true),
			};

			OnError.attributes = new[] {
				new MSBuildAttributeSyntax (OnError, "ExecuteTargets", ElementDescriptions.OnError_ExecuteTargets, MSBuildSyntaxKind.OnError_ExecuteTargets, MSBuildValueKind.TargetName.List (), true),
				new MSBuildAttributeSyntax (OnError, "Condition", ElementDescriptions.OnError_Condition, MSBuildSyntaxKind.OnError_Condition, MSBuildValueKind.Condition),
				new MSBuildAttributeSyntax (OnError, "Label", ElementDescriptions.OnError_Label, MSBuildSyntaxKind.OnError_Label, MSBuildValueKind.Label),
			};

			UsingTask.attributes = new[] {
				new MSBuildAttributeSyntax (UsingTask, "Condition", ElementDescriptions.UsingTask_Condition, MSBuildSyntaxKind.UsingTask_Condition, MSBuildValueKind.Condition),
				new MSBuildAttributeSyntax (UsingTask, "AssemblyName", ElementDescriptions.UsingTask_AssemblyName, MSBuildSyntaxKind.UsingTask_AssemblyName, MSBuildValueKind.TaskAssemblyName),
				new MSBuildAttributeSyntax (UsingTask, "AssemblyFile", ElementDescriptions.UsingTask_AssemblyFile, MSBuildSyntaxKind.UsingTask_AssemblyFile, MSBuildValueKind.TaskAssemblyFile),
				new MSBuildAttributeSyntax (UsingTask, "TaskName", ElementDescriptions.UsingTask_TaskName, MSBuildSyntaxKind.UsingTask_TaskName, MSBuildValueKind.TaskName, true),
				new MSBuildAttributeSyntax (UsingTask, "TaskFactory", ElementDescriptions.UsingTask_TaskFactory, MSBuildSyntaxKind.UsingTask_TaskFactory, MSBuildValueKind.TaskFactory),
				new MSBuildAttributeSyntax (UsingTask, "Architecture", ElementDescriptions.UsingTask_Architecture, MSBuildSyntaxKind.UsingTask_Architecture, MSBuildValueKind.TaskArchitecture),
				new MSBuildAttributeSyntax (UsingTask, "Runtime", ElementDescriptions.UsingTask_Runtime, MSBuildSyntaxKind.UsingTask_Runtime, MSBuildValueKind.TaskRuntime),
			};

			TaskBody.attributes = new[] {
				new MSBuildAttributeSyntax (TaskBody, "Evaluate", ElementDescriptions.UsingTaskBody_Evaluate, MSBuildSyntaxKind.UsingTaskBody_Evaluate, MSBuildValueKind.Bool.Literal ()),
			};

			Output.attributes = new[] {
				new MSBuildAttributeSyntax (Output, "TaskParameter", ElementDescriptions.Output_TaskParameter, MSBuildSyntaxKind.Output_TaskParameter, MSBuildValueKind.TaskOutputParameterName.Literal (), true),
				new MSBuildAttributeSyntax (Output, "Condition", ElementDescriptions.Output_Condition, MSBuildSyntaxKind.Output_Condition,  MSBuildValueKind.Condition),
				new MSBuildAttributeSyntax (Output, "ItemName", ElementDescriptions.Output_ItemName, MSBuildSyntaxKind.Output_ItemName, MSBuildValueKind.ItemName.Literal ()),
				new MSBuildAttributeSyntax (Output, "PropertyName", ElementDescriptions.Output_PropertyName, MSBuildSyntaxKind.Output_PropertyName, MSBuildValueKind.PropertyName.Literal ()),
			};

			var taskParameterAtt = new MSBuildAttributeSyntax (Task, "Parameter", ElementDescriptions.Task_Parameter, MSBuildSyntaxKind.Task_Parameter, MSBuildValueKind.Unknown, abstractKind: MSBuildSyntaxKind.Parameter);
			Task.AbstractAttribute = taskParameterAtt;

			Task.attributes = new[] {
				new MSBuildAttributeSyntax (Task, "Condition", ElementDescriptions.Task_Condition, MSBuildSyntaxKind.Task_Condition, MSBuildValueKind.Condition),
				new MSBuildAttributeSyntax (Task, "ContinueOnError", ElementDescriptions.Task_ContinueOnError, MSBuildSyntaxKind.Task_ContinueOnError, MSBuildValueKind.ContinueOnError),
				new MSBuildAttributeSyntax (Task, "Architecture", ElementDescriptions.Task_Architecture, MSBuildSyntaxKind.Task_Architecture, MSBuildValueKind.TaskArchitecture),
				new MSBuildAttributeSyntax (Task, "Runtime", ElementDescriptions.Task_Runtime, MSBuildSyntaxKind.Task_Runtime, MSBuildValueKind.TaskRuntime),
				taskParameterAtt
			};

			Metadata.attributes = new[] {
				new MSBuildAttributeSyntax (Metadata, "Condition", ElementDescriptions.Metadata_Condition, MSBuildSyntaxKind.Metadata_Condition, MSBuildValueKind.Condition),
			};
		}
	}
}