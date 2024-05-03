// Copyright (c) 2014 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using MonoDevelop.MSBuild.Language.Typesystem;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Language.Syntax
{
	[DebuggerDisplay("MSBuildElementSyntax ({SyntaxKind,nq})")]
	public class MSBuildElementSyntax : MSBuildSyntax
	{
		MSBuildElementSyntax[] children = Array.Empty<MSBuildElementSyntax> ();
		MSBuildAttributeSyntax[] attributes = Array.Empty<MSBuildAttributeSyntax> ();

		public IEnumerable<MSBuildElementSyntax> Children { get { return children; } }
		public IEnumerable<MSBuildAttributeSyntax> Attributes { get { return attributes; } }
		public MSBuildSyntaxKind SyntaxKind { get; }
		public bool IsAbstract { get; }
		public MSBuildElementSyntax AbstractChild { get; private set; }
		public MSBuildAttributeSyntax AbstractAttribute { get; private set; }

		/// <summary>
		/// Help URL for attributes of this element that do not define their own help URL
		/// </summary>
		public string? AttributesHelpUrl { get; }

		MSBuildElementSyntax (
			string name, DisplayText description, MSBuildSyntaxKind syntaxKind,
			MSBuildValueKind valueKind = MSBuildValueKind.Nothing,
			CustomTypeInfo? customType = null,
			bool isAbstract = false, SymbolVersionInfo? versionInfo = null, string helpUrl = null, string? attributesHelpUrl = null)
			: base (name, description, valueKind, customType, versionInfo, helpUrl)
		{
			SyntaxKind = syntaxKind;
			IsAbstract = isAbstract;
			AttributesHelpUrl = attributesHelpUrl;
		}

		public bool HasChild (string name)
		{
			return children != null && children.Any (c => string.Equals (name, c.Name, StringComparison.OrdinalIgnoreCase));
		}

		public static MSBuildElementSyntax Get (string name, MSBuildElementSyntax? parent = null)
		{
			if (parent != null) {
				return parent.GetChild (name);
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
					if (n is XElement xroot && xroot.Name.Equals ("Project", true)) {
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

		public static MSBuildElementSyntax Get (XElement el)
		{
			if (el.Parent is XDocument && el.Name.Equals ("Project", true)) {
				return Project;
			}
			var parentSyntax = el.Parent is XElement p ? Get (p) : null;
			if (parentSyntax != null) {
				return Get (el.Name.Name, parentSyntax);
			}
			return null;
		}

		public static (MSBuildElementSyntax element, MSBuildAttributeSyntax attribute)? Get (XObject obj)
		{
			switch (obj) {
			case XElement el:
				return (Get (el), null);
			case XAttribute att:
				if (att.Parent is XElement attEl) {
					var elementSyntax = Get (attEl);
					if (elementSyntax != null) {
						return (elementSyntax, elementSyntax.GetAttribute (att.Name.FullName));
					}
					return (elementSyntax, null);
				}
				break;
			case XText _:
			case XComment _:
				if (obj.Parent is XElement e)
					return (Get (e), null);
				break;
			}
			return null;
		}

		public MSBuildElementSyntax GetChild (string name)
		{
			foreach (var element in children) {
				if (string.Equals (element.Name, name, StringComparison.OrdinalIgnoreCase)) {
					return element;
				}
			}
			if (!string.IsNullOrEmpty (name)) {
				return AbstractChild;
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
			if (!string.IsNullOrEmpty (name)) {
				return AbstractAttribute;
			}
			return null;
		}

		static readonly Dictionary<string, MSBuildElementSyntax> builtin = new (StringComparer.OrdinalIgnoreCase);

		static MSBuildElementSyntax AddBuiltin (string name, string description, MSBuildSyntaxKind kind, MSBuildValueKind valueKind = MSBuildValueKind.Nothing, bool isAbstract = false,
			string? helpUrl = null, string? attributesHelpUrl = null)
		{
			var el = new MSBuildElementSyntax (name, description, kind, valueKind, isAbstract: isAbstract, helpUrl: helpUrl, attributesHelpUrl: attributesHelpUrl);
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
		public static MSBuildElementSyntax Sdk { get; }
		public static MSBuildElementSyntax Target { get; }
		public static MSBuildElementSyntax Task { get; }
		public static MSBuildElementSyntax TaskBody { get; }
		public static MSBuildElementSyntax UsingTask { get; }
		public static MSBuildElementSyntax When { get; }

		// this is derived from Microsoft.Build.Core.xsd
		static MSBuildElementSyntax ()
		{
			Choose = AddBuiltin ("Choose", ElementDescriptions.Choose, MSBuildSyntaxKind.Choose, helpUrl: HelpUrls.Element_Choose);
			Import = AddBuiltin ("Import", ElementDescriptions.Import, MSBuildSyntaxKind.Import, helpUrl: HelpUrls.Element_Import, attributesHelpUrl: HelpUrls.Element_Import_Attributes);
			ImportGroup = AddBuiltin ("ImportGroup", ElementDescriptions.ImportGroup, MSBuildSyntaxKind.ImportGroup, helpUrl: HelpUrls.Element_ImportGroup);
			Item = AddBuiltin ("Item", ElementDescriptions.Item, MSBuildSyntaxKind.Item, isAbstract: true, helpUrl: HelpUrls.Element_Item, attributesHelpUrl: HelpUrls.Element_Item_Attributes);
			ItemDefinition = AddBuiltin ("ItemDefinition", ElementDescriptions.ItemDefinition, MSBuildSyntaxKind.ItemDefinition, isAbstract: true); // docs don't treat this as distinct from Item in ItemGroup
			ItemDefinitionGroup = AddBuiltin ("ItemDefinitionGroup", ElementDescriptions.ItemDefinitionGroup, MSBuildSyntaxKind.ItemDefinitionGroup, helpUrl: HelpUrls.Element_ItemDefinitionGroup);
			ItemGroup = AddBuiltin ("ItemGroup", ElementDescriptions.ItemGroup, MSBuildSyntaxKind.ItemGroup, helpUrl: HelpUrls.Element_ItemGroup, attributesHelpUrl: HelpUrls.Element_ItemGroup_Attributes);
			Metadata = AddBuiltin ("Metadata", ElementDescriptions.Metadata, MSBuildSyntaxKind.Metadata, MSBuildValueKind.Unknown, isAbstract: true, helpUrl: HelpUrls.Element_Metadata);
			OnError = AddBuiltin ("OnError", ElementDescriptions.OnError, MSBuildSyntaxKind.OnError, helpUrl: HelpUrls.Element_OnError, attributesHelpUrl: HelpUrls.Element_OnError_Attributes);
			Otherwise = AddBuiltin ("Otherwise", ElementDescriptions.Otherwise, MSBuildSyntaxKind.Otherwise, helpUrl: HelpUrls.Element_Otherwise);
			Output = AddBuiltin ("Output", ElementDescriptions.Output, MSBuildSyntaxKind.Output, helpUrl: HelpUrls.Element_Output, attributesHelpUrl: HelpUrls.Element_Output_Attributes);
			Parameter = AddBuiltin ("Parameter", ElementDescriptions.Parameter, MSBuildSyntaxKind.Parameter, isAbstract: true, helpUrl: HelpUrls.Element_Parameter, attributesHelpUrl: HelpUrls.Element_Parameter_Attributes);
			ParameterGroup = AddBuiltin ("ParameterGroup", ElementDescriptions.ParameterGroup, MSBuildSyntaxKind.ParameterGroup, helpUrl: HelpUrls.Element_ParameterGroup);
			Project = AddBuiltin ("Project", ElementDescriptions.Project, MSBuildSyntaxKind.Project, helpUrl: HelpUrls.Element_Project, attributesHelpUrl: HelpUrls.Element_Project_Attributes);
			ProjectExtensions = AddBuiltin ("ProjectExtensions", ElementDescriptions.ProjectExtensions, MSBuildSyntaxKind.ProjectExtensions, MSBuildValueKind.Data, helpUrl: HelpUrls.Element_ProjectExtensions);
			Property = AddBuiltin ("Property", ElementDescriptions.Property, MSBuildSyntaxKind.Property, MSBuildValueKind.Unknown, isAbstract: true, helpUrl: HelpUrls.Element_Property);
			PropertyGroup = AddBuiltin ("PropertyGroup", ElementDescriptions.PropertyGroup, MSBuildSyntaxKind.PropertyGroup, helpUrl: HelpUrls.Element_PropertyGroup);
			Sdk = AddBuiltin ("Sdk", ElementDescriptions.Sdk, MSBuildSyntaxKind.Sdk, helpUrl: HelpUrls.Element_Sdk, attributesHelpUrl: HelpUrls.Element_Sdk_Attributes);
			Target = AddBuiltin ("Target", ElementDescriptions.Target, MSBuildSyntaxKind.Target, helpUrl: HelpUrls.Element_Target, attributesHelpUrl: HelpUrls.Element_Target_Attributes);
			Task = AddBuiltin ("AbstractTask", ElementDescriptions.Task, MSBuildSyntaxKind.Task, isAbstract:true, helpUrl: HelpUrls.Element_Task, attributesHelpUrl: HelpUrls.Element_Task_Attributes);
			TaskBody = AddBuiltin ("Task", ElementDescriptions.TaskBody, MSBuildSyntaxKind.TaskBody, helpUrl: HelpUrls.Element_TaskBody, attributesHelpUrl: HelpUrls.Element_TaskBody_Attributes);
			UsingTask = AddBuiltin ("UsingTask", ElementDescriptions.UsingTask, MSBuildSyntaxKind.UsingTask, helpUrl: HelpUrls.Element_UsingTask, attributesHelpUrl: HelpUrls.Element_UsingTask_Attributes);
			When = AddBuiltin ("When", ElementDescriptions.When, MSBuildSyntaxKind.When, helpUrl: HelpUrls.Element_When);

			Choose.children = [Otherwise, When];
			ImportGroup.children = [Import];
			Item.children = [Metadata];
			ItemDefinition.children = [Metadata];
			ItemDefinitionGroup.children = [ItemDefinition];
			ItemGroup.children = [Item];
			Otherwise.children = [Choose, ItemGroup, PropertyGroup];
			ParameterGroup.children = [Parameter];
			Project.children = [Choose, Import, ImportGroup, ProjectExtensions, PropertyGroup, ItemGroup, ItemDefinitionGroup, Target, UsingTask, Sdk];
			PropertyGroup.children = [Property];
			Target.children = [OnError, ItemGroup, PropertyGroup, Task];
			Task.children = [Output];
			UsingTask.children = [ParameterGroup, TaskBody];
			When.children = [Choose, ItemGroup, PropertyGroup];

			Item.AbstractChild = Metadata;
			Target.AbstractChild = Task;
			ItemDefinitionGroup.AbstractChild = ItemDefinition;
			ItemDefinition.AbstractChild = Metadata;
			PropertyGroup.AbstractChild = Property;
			ItemGroup.AbstractChild = Item;
			ParameterGroup.AbstractChild = Parameter;

			Import.attributes = [
				new (Import, "Project", ElementDescriptions.Import_Project, MSBuildSyntaxKind.Import_Project, MSBuildValueKind.ProjectFile, required: true),
				new (Import, "Condition", ElementDescriptions.Import_Condition, MSBuildSyntaxKind.Import_Condition, MSBuildValueKind.Condition, helpUrl: HelpUrls.Attribute_Condition),
				new (Import, "Label", ElementDescriptions.Import_Label, MSBuildSyntaxKind.Import_Label, MSBuildValueKind.Label),
				new (Import, "Sdk", ElementDescriptions.Import_Sdk, MSBuildSyntaxKind.Import_Sdk, MSBuildValueKind.Sdk),
				new (Import, "Version", ElementDescriptions.Import_Version, MSBuildSyntaxKind.Import_Version, MSBuildValueKind.SdkVersion),
				new (Import, "MinimumVersion", ElementDescriptions.Import_MinimumVersion, MSBuildSyntaxKind.Import_MinimumVersion, MSBuildValueKind.SdkVersion),
			];

			var itemMetadataAtt = new MSBuildAttributeSyntax (Item, "Metadata", ElementDescriptions.Metadata, MSBuildSyntaxKind.Item_Metadata, MSBuildValueKind.Unknown, abstractKind: MSBuildSyntaxKind.Metadata);
			Item.AbstractAttribute = itemMetadataAtt;

			Item.attributes = [
				new (Item, "Exclude", ElementDescriptions.Item_Exclude, MSBuildSyntaxKind.Item_Exclude, MSBuildValueKind.MatchItem, helpUrl: HelpUrls.Attribute_Item_Include),
				new (Item, "Include", ElementDescriptions.Item_Include, MSBuildSyntaxKind.Item_Include, MSBuildValueKind.MatchItem, helpUrl: HelpUrls.Attribute_Item_Exclude),
				new (Item, "Remove", ElementDescriptions.Item_Remove, MSBuildSyntaxKind.Item_Remove, MSBuildValueKind.MatchItem),
				new (Item, "Update", ElementDescriptions.Item_Update, MSBuildSyntaxKind.Item_Update, MSBuildValueKind.MatchItem),
				new (Item, "Condition", ElementDescriptions.Item_Condition, MSBuildSyntaxKind.Item_Condition, MSBuildValueKind.Condition, helpUrl: HelpUrls.Attribute_Condition),
				new (Item, "Label", ElementDescriptions.Item_Label, MSBuildSyntaxKind.Item_Label, MSBuildValueKind.Label),
				new (Item, "KeepMetadata", ElementDescriptions.Item_KeepMetadata, MSBuildSyntaxKind.Item_KeepMetadata, MSBuildValueKind.MetadataName.AsList ()),
				new (Item, "RemoveMetadata", ElementDescriptions.Item_RemoveMetadata, MSBuildSyntaxKind.Item_RemoveMetadata, MSBuildValueKind.MetadataName.AsList ()),
				new (Item, "KeepDuplicates", ElementDescriptions.Item_KeepDuplicates, MSBuildSyntaxKind.Parameter_Required, MSBuildValueKind.Bool),
				itemMetadataAtt
			];

			Parameter.attributes = [
				new (Parameter, "Output", ElementDescriptions.Parameter_Output, MSBuildSyntaxKind.Parameter_Output, MSBuildValueKind.Bool.AsLiteral()),
				new (Parameter, "ParameterType", ElementDescriptions.Parameter_ParameterType, MSBuildSyntaxKind.Parameter_ParameterType, MSBuildValueKind.TaskParameterType),
				new (Parameter, "Required", ElementDescriptions.Parameter_Required, MSBuildSyntaxKind.Parameter_Required, MSBuildValueKind.Bool.AsLiteral()),
			];

			Project.attributes = [
				new (Project, "DefaultTargets", ElementDescriptions.Project_DefaultTargets, MSBuildSyntaxKind.Project_DefaultTargets, MSBuildValueKind.TargetName.AsList ().AsLiteral (),
					helpUrl: "https://learn.microsoft.com/en-us/visualstudio/msbuild/target-build-order#default-targets"
				),
				new (Project, "InitialTargets", ElementDescriptions.Project_InitialTargets, MSBuildSyntaxKind.Project_InitialTargets, MSBuildValueKind.TargetName.AsList ().AsLiteral (),
					helpUrl: "https://learn.microsoft.com/en-us/visualstudio/msbuild/target-build-order#initial-targets"
				),
				new (Project, "ToolsVersion", ElementDescriptions.Project_ToolsVersion, MSBuildSyntaxKind.Project_ToolsVersion, MSBuildValueKind.ToolsVersion.AsLiteral (), versionInfo: MSBuildIntrinsics.ToolsVersionDeprecatedInfo, helpUrl: HelpUrls.Element_Project_ToolsVersion),
				new (Project, "TreatAsLocalProperty", ElementDescriptions.Project_TreatAsLocalProperty, MSBuildSyntaxKind.Project_TreatAsLocalProperty, MSBuildValueKind.PropertyName.AsList ().AsLiteral ()),
				new (Project, "xmlns", ElementDescriptions.Project_xmlns, MSBuildSyntaxKind.Project_xmlns, MSBuildValueKind.Xmlns.AsLiteral ()),
				new (Project, "Sdk", ElementDescriptions.Project_Sdk, MSBuildSyntaxKind.Project_Sdk, MSBuildValueKind.SdkWithVersion.AsList().AsLiteral ()),
			];

			Sdk.attributes = [
				new (Project, "Name", ElementDescriptions.Sdk_Name, MSBuildSyntaxKind.Sdk_Name, MSBuildValueKind.Sdk, required: true),
				new (Project, "Version", ElementDescriptions.Sdk_Version, MSBuildSyntaxKind.Sdk_Version, MSBuildValueKind.SdkVersion)
			];

			Target.attributes = [
				new (Target, "Name", ElementDescriptions.Target_Name, MSBuildSyntaxKind.Target_Name, MSBuildValueKind.TargetName.AsLiteral (), required: true),
				new (Target, "DependsOnTargets", ElementDescriptions.Target_DependsOnTargets, MSBuildSyntaxKind.Target_DependsOnTargets, MSBuildValueKind.TargetName.AsList (),
					helpUrl: "https://learn.microsoft.com/en-us/visualstudio/msbuild/target-build-order#target-dependencies"
				),
				new (Target, "Inputs", ElementDescriptions.Target_Inputs, MSBuildSyntaxKind.Target_Inputs, MSBuildValueKind.Unknown),
				new (Target, "Outputs", ElementDescriptions.Target_Outputs, MSBuildSyntaxKind.Target_Outputs, MSBuildValueKind.Unknown),
				new (Target, "Condition", ElementDescriptions.Target_Condition, MSBuildSyntaxKind.Target_Condition, MSBuildValueKind.Condition, helpUrl : HelpUrls.Attribute_Condition),
				new (Target, "KeepDuplicateOutputs", ElementDescriptions.Target_KeepDuplicateOutputs, MSBuildSyntaxKind.Target_KeepDuplicateOutputs, MSBuildValueKind.Bool),
				new (Target, "Returns", ElementDescriptions.Target_Returns, MSBuildSyntaxKind.Target_Returns, MSBuildValueKind.Unknown),
				new (Target, "BeforeTargets", ElementDescriptions.Target_BeforeTargets, MSBuildSyntaxKind.Target_BeforeTargets, MSBuildValueKind.TargetName.AsList (),
					helpUrl: "https://learn.microsoft.com/en-us/visualstudio/msbuild/target-build-order#beforetargets-and-aftertargets"
				),
				new (Target, "AfterTargets", ElementDescriptions.Target_AfterTargets, MSBuildSyntaxKind.Target_AfterTargets, MSBuildValueKind.TargetName.AsList (),
					helpUrl: "https://learn.microsoft.com/en-us/visualstudio/msbuild/target-build-order#beforetargets-and-aftertargets"
				),
				new (Target, "Label", ElementDescriptions.Target_Label, MSBuildSyntaxKind.Target_Label, MSBuildValueKind.Label),
			];

			Property.attributes = [
				new (Property, "Label", ElementDescriptions.Property_Label, MSBuildSyntaxKind.Property_Label, MSBuildValueKind.Label),
				new (Property, "Condition", ElementDescriptions.Property_Condition, MSBuildSyntaxKind.Property_Condition, MSBuildValueKind.Condition, helpUrl : HelpUrls.Attribute_Condition),
			];

			PropertyGroup.attributes = [
				new (PropertyGroup, "Label", ElementDescriptions.PropertyGroup_Label, MSBuildSyntaxKind.PropertyGroup_Label, MSBuildValueKind.Label),
				new (PropertyGroup, "Condition", ElementDescriptions.PropertyGroup_Condition, MSBuildSyntaxKind.PropertyGroup_Condition, MSBuildValueKind.Condition, helpUrl : HelpUrls.Attribute_Condition),
			];

			ImportGroup.attributes = [
				new (ImportGroup, "Label", ElementDescriptions.ImportGroup_Label, MSBuildSyntaxKind.ImportGroup_Label, MSBuildValueKind.Label),
				new (ImportGroup, "Condition", ElementDescriptions.ImportGroup_Condition, MSBuildSyntaxKind.ImportGroup_Condition, MSBuildValueKind.Condition, helpUrl : HelpUrls.Attribute_Condition),
			];

			ItemGroup.attributes = [
				new (ItemGroup, "Label", ElementDescriptions.ItemGroup_Label, MSBuildSyntaxKind.ItemGroup_Label, MSBuildValueKind.Label),
				new (ItemGroup, "Condition", ElementDescriptions.ItemGroup_Condition, MSBuildSyntaxKind.ItemGroup_Condition, MSBuildValueKind.Condition, helpUrl : HelpUrls.Attribute_Condition),
			];

			ItemDefinitionGroup.attributes = [
				new (ItemDefinitionGroup, "Label", ElementDescriptions.ItemDefinitionGroup_Label, MSBuildSyntaxKind.ItemDefinitionGroup_Label, MSBuildValueKind.Label),
				new (ItemDefinitionGroup, "Condition", ElementDescriptions.ItemDefinitionGroup_Condition, MSBuildSyntaxKind.ItemDefinitionGroup_Condition, MSBuildValueKind.Condition, helpUrl : HelpUrls.Attribute_Condition),
			];

			When.attributes = [
				new (When, "Condition", ElementDescriptions.When_Condition, MSBuildSyntaxKind.When_Condition, MSBuildValueKind.Condition, required : true, helpUrl: HelpUrls.Attribute_Condition),
			];

			OnError.attributes = [
				new (OnError, "ExecuteTargets", ElementDescriptions.OnError_ExecuteTargets, MSBuildSyntaxKind.OnError_ExecuteTargets, MSBuildValueKind.TargetName.AsList (), required : true),
				new (OnError, "Condition", ElementDescriptions.OnError_Condition, MSBuildSyntaxKind.OnError_Condition, MSBuildValueKind.Condition, helpUrl : HelpUrls.Attribute_Condition),
				new (OnError, "Label", ElementDescriptions.OnError_Label, MSBuildSyntaxKind.OnError_Label, MSBuildValueKind.Label),
			];

			UsingTask.attributes = [
				new (UsingTask, "Condition", ElementDescriptions.UsingTask_Condition, MSBuildSyntaxKind.UsingTask_Condition, MSBuildValueKind.Condition, helpUrl : HelpUrls.Attribute_Condition),
				new (UsingTask, "AssemblyName", ElementDescriptions.UsingTask_AssemblyName, MSBuildSyntaxKind.UsingTask_AssemblyName, MSBuildValueKind.TaskAssemblyName),
				new (UsingTask, "AssemblyFile", ElementDescriptions.UsingTask_AssemblyFile, MSBuildSyntaxKind.UsingTask_AssemblyFile, MSBuildValueKind.TaskAssemblyFile),
				new (UsingTask, "TaskName", ElementDescriptions.UsingTask_TaskName, MSBuildSyntaxKind.UsingTask_TaskName, MSBuildValueKind.TaskName, required: true),
				new (UsingTask, "TaskFactory", ElementDescriptions.UsingTask_TaskFactory, MSBuildSyntaxKind.UsingTask_TaskFactory, MSBuildValueKind.TaskFactory),
				new (UsingTask, "Architecture", ElementDescriptions.UsingTask_Architecture, MSBuildSyntaxKind.UsingTask_Architecture, MSBuildValueKind.TaskArchitecture),
				new (UsingTask, "Runtime", ElementDescriptions.UsingTask_Runtime, MSBuildSyntaxKind.UsingTask_Runtime, MSBuildValueKind.TaskRuntime),
			];

			TaskBody.attributes = [
				new (TaskBody, "Evaluate", ElementDescriptions.UsingTaskBody_Evaluate, MSBuildSyntaxKind.UsingTaskBody_Evaluate, MSBuildValueKind.Bool.AsLiteral ()),
			];

			Output.attributes = [
				new (Output, "TaskParameter", ElementDescriptions.Output_TaskParameter, MSBuildSyntaxKind.Output_TaskParameter, MSBuildValueKind.TaskOutputParameterName.AsLiteral (), required : true),
				new (Output, "Condition", ElementDescriptions.Output_Condition, MSBuildSyntaxKind.Output_Condition,  MSBuildValueKind.Condition, helpUrl : HelpUrls.Attribute_Condition),
				new (Output, "ItemName", ElementDescriptions.Output_ItemName, MSBuildSyntaxKind.Output_ItemName, MSBuildValueKind.ItemName.AsLiteral ()),
				new (Output, "PropertyName", ElementDescriptions.Output_PropertyName, MSBuildSyntaxKind.Output_PropertyName, MSBuildValueKind.PropertyName.AsLiteral ()),
			];

			var taskParameterAtt = new MSBuildAttributeSyntax (Task, "Parameter", ElementDescriptions.Task_Parameter, MSBuildSyntaxKind.Task_Parameter, MSBuildValueKind.Unknown, abstractKind: MSBuildSyntaxKind.Parameter);
			Task.AbstractAttribute = taskParameterAtt;

			Task.attributes = [
				new (Task, "Condition", ElementDescriptions.Task_Condition, MSBuildSyntaxKind.Task_Condition, MSBuildValueKind.Condition, helpUrl : HelpUrls.Attribute_Condition),
				new (Task, "ContinueOnError", ElementDescriptions.Task_ContinueOnError, MSBuildSyntaxKind.Task_ContinueOnError, MSBuildValueKind.ContinueOnError),
				new (Task, "Architecture", ElementDescriptions.Task_Architecture, MSBuildSyntaxKind.Task_Architecture, MSBuildValueKind.TaskArchitecture),
				new (Task, "Runtime", ElementDescriptions.Task_Runtime, MSBuildSyntaxKind.Task_Runtime, MSBuildValueKind.TaskRuntime),
				taskParameterAtt
			];

			Metadata.attributes = [
				new (Metadata, "Condition", ElementDescriptions.Metadata_Condition, MSBuildSyntaxKind.Metadata_Condition, MSBuildValueKind.Condition, helpUrl: HelpUrls.Attribute_Condition),
			];
		}
	}
}