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

using ElementName = MonoDevelop.MSBuild.Language.Syntax.MSBuildElementName;
using AttributeName = MonoDevelop.MSBuild.Language.Syntax.MSBuildAttributeName;

namespace MonoDevelop.MSBuild.Language.Syntax
{
	[DebuggerDisplay ("MSBuildElementSyntax ({SyntaxKind,nq})")]
	public partial class MSBuildElementSyntax : MSBuildSyntax
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

			allConcreteElements.TryGetValue (name, out MSBuildElementSyntax result);
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
					if (n is XElement xroot && xroot.Name.Equals (ElementName.Project, true)) {
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
			if (el.Parent is XDocument && el.Name.Equals (ElementName.Project, true)) {
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

		static readonly Dictionary<string, MSBuildElementSyntax> allConcreteElements = new (StringComparer.OrdinalIgnoreCase);

		static MSBuildElementSyntax Create (string name, string description, MSBuildSyntaxKind kind, MSBuildValueKind valueKind = MSBuildValueKind.Nothing, bool isAbstract = false,
			string? helpUrl = null, string? attributesHelpUrl = null)
		{
			var el = new MSBuildElementSyntax (name, description, kind, valueKind, isAbstract: isAbstract, helpUrl: helpUrl, attributesHelpUrl: attributesHelpUrl);
			if (name.Length != 0) {
				Debug.Assert (!isAbstract);
				allConcreteElements.Add (el.Name, el);
			} else {
				Debug.Assert (isAbstract);
			}
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
			// note: abstract elements and attributes have "" for the name, as the actual name could be anything
			const string ABSTRACT = "";

			Choose = Create (ElementName.Choose, ElementDescriptions.Choose, MSBuildSyntaxKind.Choose, helpUrl: HelpUrls.Element_Choose);
			Import = Create (ElementName.Import, ElementDescriptions.Import, MSBuildSyntaxKind.Import, helpUrl: HelpUrls.Element_Import, attributesHelpUrl: HelpUrls.Element_Import_Attributes);
			ImportGroup = Create (ElementName.ImportGroup, ElementDescriptions.ImportGroup, MSBuildSyntaxKind.ImportGroup, helpUrl: HelpUrls.Element_ImportGroup);
			Item = Create (ABSTRACT, ElementDescriptions.Item, MSBuildSyntaxKind.Item, isAbstract: true, helpUrl: HelpUrls.Element_Item, attributesHelpUrl: HelpUrls.Element_Item_Attributes);
			ItemDefinition = Create (ABSTRACT, ElementDescriptions.ItemDefinition, MSBuildSyntaxKind.ItemDefinition, isAbstract: true); // docs don't treat this as distinct from Item in ItemGroup
			ItemDefinitionGroup = Create (ElementName.ItemDefinitionGroup, ElementDescriptions.ItemDefinitionGroup, MSBuildSyntaxKind.ItemDefinitionGroup, helpUrl: HelpUrls.Element_ItemDefinitionGroup);
			ItemGroup = Create (ElementName.ItemGroup, ElementDescriptions.ItemGroup, MSBuildSyntaxKind.ItemGroup, helpUrl: HelpUrls.Element_ItemGroup, attributesHelpUrl: HelpUrls.Element_ItemGroup_Attributes);
			Metadata = Create (ABSTRACT, ElementDescriptions.Metadata, MSBuildSyntaxKind.Metadata, MSBuildValueKind.Unknown, isAbstract: true, helpUrl: HelpUrls.Element_Metadata);
			OnError = Create (ElementName.OnError, ElementDescriptions.OnError, MSBuildSyntaxKind.OnError, helpUrl: HelpUrls.Element_OnError, attributesHelpUrl: HelpUrls.Element_OnError_Attributes);
			Otherwise = Create (ElementName.Otherwise, ElementDescriptions.Otherwise, MSBuildSyntaxKind.Otherwise, helpUrl: HelpUrls.Element_Otherwise);
			Output = Create (ElementName.Output, ElementDescriptions.Output, MSBuildSyntaxKind.Output, helpUrl: HelpUrls.Element_Output, attributesHelpUrl: HelpUrls.Element_Output_Attributes);
			Parameter = Create (ABSTRACT, ElementDescriptions.Parameter, MSBuildSyntaxKind.Parameter, isAbstract: true, helpUrl: HelpUrls.Element_Parameter, attributesHelpUrl: HelpUrls.Element_Parameter_Attributes);
			ParameterGroup = Create (ElementName.ParameterGroup, ElementDescriptions.ParameterGroup, MSBuildSyntaxKind.ParameterGroup, helpUrl: HelpUrls.Element_ParameterGroup);
			Project = Create (ElementName.Project, ElementDescriptions.Project, MSBuildSyntaxKind.Project, helpUrl: HelpUrls.Element_Project, attributesHelpUrl: HelpUrls.Element_Project_Attributes);
			ProjectExtensions = Create (ElementName.ProjectExtensions, ElementDescriptions.ProjectExtensions, MSBuildSyntaxKind.ProjectExtensions, MSBuildValueKind.Data, helpUrl: HelpUrls.Element_ProjectExtensions);
			Property = Create (ABSTRACT, ElementDescriptions.Property, MSBuildSyntaxKind.Property, MSBuildValueKind.Unknown, isAbstract: true, helpUrl: HelpUrls.Element_Property);
			PropertyGroup = Create (ElementName.PropertyGroup, ElementDescriptions.PropertyGroup, MSBuildSyntaxKind.PropertyGroup, helpUrl: HelpUrls.Element_PropertyGroup);
			Sdk = Create (ElementName.Sdk, ElementDescriptions.Sdk, MSBuildSyntaxKind.Sdk, helpUrl: HelpUrls.Element_Sdk, attributesHelpUrl: HelpUrls.Element_Sdk_Attributes);
			Target = Create (ElementName.Target, ElementDescriptions.Target, MSBuildSyntaxKind.Target, helpUrl: HelpUrls.Element_Target, attributesHelpUrl: HelpUrls.Element_Target_Attributes);
			Task = Create (ABSTRACT, ElementDescriptions.Task, MSBuildSyntaxKind.Task, isAbstract: true, helpUrl: HelpUrls.Element_Task, attributesHelpUrl: HelpUrls.Element_Task_Attributes);
			TaskBody = Create (ElementName.Task, ElementDescriptions.TaskBody, MSBuildSyntaxKind.TaskBody, helpUrl: HelpUrls.Element_TaskBody, attributesHelpUrl: HelpUrls.Element_TaskBody_Attributes);
			UsingTask = Create (ElementName.UsingTask, ElementDescriptions.UsingTask, MSBuildSyntaxKind.UsingTask, helpUrl: HelpUrls.Element_UsingTask, attributesHelpUrl: HelpUrls.Element_UsingTask_Attributes);
			When = Create (ElementName.When, ElementDescriptions.When, MSBuildSyntaxKind.When, helpUrl: HelpUrls.Element_When);

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
				new (Import, AttributeName.Project, ElementDescriptions.Import_Project, MSBuildSyntaxKind.Import_Project, MSBuildValueKind.ProjectFile, required: true),
				new (Import, AttributeName.Condition, ElementDescriptions.Import_Condition, MSBuildSyntaxKind.Import_Condition, MSBuildValueKind.Condition, helpUrl: HelpUrls.Attribute_Condition),
				new (Import, AttributeName.Label, ElementDescriptions.Import_Label, MSBuildSyntaxKind.Import_Label, MSBuildValueKind.Label),
				new (Import, AttributeName.Sdk, ElementDescriptions.Import_Sdk, MSBuildSyntaxKind.Import_Sdk, MSBuildValueKind.Sdk),
				new (Import, AttributeName.Version, ElementDescriptions.Import_Version, MSBuildSyntaxKind.Import_Version, MSBuildValueKind.SdkVersion),
				new (Import, AttributeName.MinimumVersion, ElementDescriptions.Import_MinimumVersion, MSBuildSyntaxKind.Import_MinimumVersion, MSBuildValueKind.SdkVersion),
			];

			var itemMetadataAtt = new MSBuildAttributeSyntax (Item, ABSTRACT, ElementDescriptions.Metadata, MSBuildSyntaxKind.Item_Metadata, MSBuildValueKind.Unknown, abstractKind: MSBuildSyntaxKind.Metadata);
			Item.AbstractAttribute = itemMetadataAtt;

			Item.attributes = [
				new (Item, AttributeName.Exclude, ElementDescriptions.Item_Exclude, MSBuildSyntaxKind.Item_Exclude, MSBuildValueKind.MatchItem, helpUrl: HelpUrls.Attribute_Item_Include),
				new (Item, AttributeName.Include, ElementDescriptions.Item_Include, MSBuildSyntaxKind.Item_Include, MSBuildValueKind.MatchItem, helpUrl: HelpUrls.Attribute_Item_Exclude),
				new (Item, AttributeName.Remove, ElementDescriptions.Item_Remove, MSBuildSyntaxKind.Item_Remove, MSBuildValueKind.MatchItem),
				new (Item, AttributeName.Update, ElementDescriptions.Item_Update, MSBuildSyntaxKind.Item_Update, MSBuildValueKind.MatchItem),
				new (Item, AttributeName.Condition, ElementDescriptions.Item_Condition, MSBuildSyntaxKind.Item_Condition, MSBuildValueKind.Condition, helpUrl: HelpUrls.Attribute_Condition),
				new (Item, AttributeName.Label, ElementDescriptions.Item_Label, MSBuildSyntaxKind.Item_Label, MSBuildValueKind.Label),
				new (Item, AttributeName.KeepMetadata, ElementDescriptions.Item_KeepMetadata, MSBuildSyntaxKind.Item_KeepMetadata, MSBuildValueKind.MetadataName.AsList ()),
				new (Item, AttributeName.RemoveMetadata, ElementDescriptions.Item_RemoveMetadata, MSBuildSyntaxKind.Item_RemoveMetadata, MSBuildValueKind.MetadataName.AsList ()),
				new (Item, AttributeName.KeepDuplicates, ElementDescriptions.Item_KeepDuplicates, MSBuildSyntaxKind.Item_KeepDuplicates, MSBuildValueKind.Bool),
				itemMetadataAtt
			];

			Parameter.attributes = [
				new (Parameter, AttributeName.Output, ElementDescriptions.Parameter_Output, MSBuildSyntaxKind.Parameter_Output, MSBuildValueKind.Bool.AsLiteral()),
				new (Parameter, AttributeName.ParameterType, ElementDescriptions.Parameter_ParameterType, MSBuildSyntaxKind.Parameter_ParameterType, MSBuildValueKind.TaskParameterType),
				new (Parameter, AttributeName.Required, ElementDescriptions.Parameter_Required, MSBuildSyntaxKind.Parameter_Required, MSBuildValueKind.Bool.AsLiteral()),
			];

			Project.attributes = [
				new (Project, AttributeName.DefaultTargets, ElementDescriptions.Project_DefaultTargets, MSBuildSyntaxKind.Project_DefaultTargets, MSBuildValueKind.TargetName.AsList ().AsLiteral (),
					helpUrl: "https://learn.microsoft.com/en-us/visualstudio/msbuild/target-build-order#default-targets"
				),
				new (Project, AttributeName.InitialTargets, ElementDescriptions.Project_InitialTargets, MSBuildSyntaxKind.Project_InitialTargets, MSBuildValueKind.TargetName.AsList ().AsLiteral (),
					helpUrl: "https://learn.microsoft.com/en-us/visualstudio/msbuild/target-build-order#initial-targets"
				),
				new (Project, AttributeName.ToolsVersion, ElementDescriptions.Project_ToolsVersion, MSBuildSyntaxKind.Project_ToolsVersion, MSBuildValueKind.ToolsVersion.AsLiteral (), versionInfo: MSBuildIntrinsics.ToolsVersionDeprecatedInfo, helpUrl: HelpUrls.Element_Project_ToolsVersion),
				new (Project, AttributeName.TreatAsLocalProperty, ElementDescriptions.Project_TreatAsLocalProperty, MSBuildSyntaxKind.Project_TreatAsLocalProperty, MSBuildValueKind.PropertyName.AsList ().AsLiteral ()),
				new (Project, AttributeName.xmlns, ElementDescriptions.Project_xmlns, MSBuildSyntaxKind.Project_xmlns, MSBuildValueKind.Xmlns.AsLiteral ()),
				new (Project, AttributeName.Sdk, ElementDescriptions.Project_Sdk, MSBuildSyntaxKind.Project_Sdk, MSBuildValueKind.SdkWithVersion.AsList().AsLiteral ()),
			];

			Sdk.attributes = [
				new (Project, AttributeName.Name, ElementDescriptions.Sdk_Name, MSBuildSyntaxKind.Sdk_Name, MSBuildValueKind.Sdk, required: true),
				new (Project, AttributeName.Version, ElementDescriptions.Sdk_Version, MSBuildSyntaxKind.Sdk_Version, MSBuildValueKind.SdkVersion),
			];

			Target.attributes = [
				new (Target, AttributeName.Name, ElementDescriptions.Target_Name, MSBuildSyntaxKind.Target_Name, MSBuildValueKind.TargetName.AsLiteral (), required: true),
				new (Target, AttributeName.DependsOnTargets, ElementDescriptions.Target_DependsOnTargets, MSBuildSyntaxKind.Target_DependsOnTargets, MSBuildValueKind.TargetName.AsList (),
					helpUrl: "https://learn.microsoft.com/en-us/visualstudio/msbuild/target-build-order#target-dependencies"
				),
				new (Target, AttributeName.Inputs, ElementDescriptions.Target_Inputs, MSBuildSyntaxKind.Target_Inputs, MSBuildValueKind.Unknown),
				new (Target, AttributeName.Outputs, ElementDescriptions.Target_Outputs, MSBuildSyntaxKind.Target_Outputs, MSBuildValueKind.Unknown),
				new (Target, AttributeName.Condition, ElementDescriptions.Target_Condition, MSBuildSyntaxKind.Target_Condition, MSBuildValueKind.Condition, helpUrl : HelpUrls.Attribute_Condition),
				new (Target, AttributeName.KeepDuplicateOutputs, ElementDescriptions.Target_KeepDuplicateOutputs, MSBuildSyntaxKind.Target_KeepDuplicateOutputs, MSBuildValueKind.Bool),
				new (Target, AttributeName.Returns, ElementDescriptions.Target_Returns, MSBuildSyntaxKind.Target_Returns, MSBuildValueKind.Unknown),
				new (Target, AttributeName.BeforeTargets, ElementDescriptions.Target_BeforeTargets, MSBuildSyntaxKind.Target_BeforeTargets, MSBuildValueKind.TargetName.AsList (),
					helpUrl: "https://learn.microsoft.com/en-us/visualstudio/msbuild/target-build-order#beforetargets-and-aftertargets"
				),
				new (Target, AttributeName.AfterTargets, ElementDescriptions.Target_AfterTargets, MSBuildSyntaxKind.Target_AfterTargets, MSBuildValueKind.TargetName.AsList (),
					helpUrl: "https://learn.microsoft.com/en-us/visualstudio/msbuild/target-build-order#beforetargets-and-aftertargets"
				),
				new (Target, AttributeName.Label, ElementDescriptions.Target_Label, MSBuildSyntaxKind.Target_Label, MSBuildValueKind.Label),
			];

			Property.attributes = [
				new (Property, AttributeName.Label, ElementDescriptions.Property_Label, MSBuildSyntaxKind.Property_Label, MSBuildValueKind.Label),
				new (Property, AttributeName.Condition, ElementDescriptions.Property_Condition, MSBuildSyntaxKind.Property_Condition, MSBuildValueKind.Condition, helpUrl : HelpUrls.Attribute_Condition),
			];

			PropertyGroup.attributes = [
				new (PropertyGroup, AttributeName.Label, ElementDescriptions.PropertyGroup_Label, MSBuildSyntaxKind.PropertyGroup_Label, MSBuildValueKind.Label),
				new (PropertyGroup, AttributeName.Condition, ElementDescriptions.PropertyGroup_Condition, MSBuildSyntaxKind.PropertyGroup_Condition, MSBuildValueKind.Condition, helpUrl : HelpUrls.Attribute_Condition),
			];

			ImportGroup.attributes = [
				new (ImportGroup, AttributeName.Label, ElementDescriptions.ImportGroup_Label, MSBuildSyntaxKind.ImportGroup_Label, MSBuildValueKind.Label),
				new (ImportGroup, AttributeName.Condition, ElementDescriptions.ImportGroup_Condition, MSBuildSyntaxKind.ImportGroup_Condition, MSBuildValueKind.Condition, helpUrl : HelpUrls.Attribute_Condition),
			];

			ItemGroup.attributes = [
				new (ItemGroup, AttributeName.Label, ElementDescriptions.ItemGroup_Label, MSBuildSyntaxKind.ItemGroup_Label, MSBuildValueKind.Label),
				new (ItemGroup, AttributeName.Condition, ElementDescriptions.ItemGroup_Condition, MSBuildSyntaxKind.ItemGroup_Condition, MSBuildValueKind.Condition, helpUrl : HelpUrls.Attribute_Condition),
			];

			ItemDefinitionGroup.attributes = [
				new (ItemDefinitionGroup, AttributeName.Label, ElementDescriptions.ItemDefinitionGroup_Label, MSBuildSyntaxKind.ItemDefinitionGroup_Label, MSBuildValueKind.Label),
				new (ItemDefinitionGroup, AttributeName.Condition, ElementDescriptions.ItemDefinitionGroup_Condition, MSBuildSyntaxKind.ItemDefinitionGroup_Condition, MSBuildValueKind.Condition, helpUrl : HelpUrls.Attribute_Condition),
			];

			When.attributes = [
				new (When, AttributeName.Condition, ElementDescriptions.When_Condition, MSBuildSyntaxKind.When_Condition, MSBuildValueKind.Condition, required : true, helpUrl: HelpUrls.Attribute_Condition),
			];

			OnError.attributes = [
				new (OnError, AttributeName.ExecuteTargets, ElementDescriptions.OnError_ExecuteTargets, MSBuildSyntaxKind.OnError_ExecuteTargets, MSBuildValueKind.TargetName.AsList (), required : true),
				new (OnError, AttributeName.Condition, ElementDescriptions.OnError_Condition, MSBuildSyntaxKind.OnError_Condition, MSBuildValueKind.Condition, helpUrl : HelpUrls.Attribute_Condition),
				new (OnError, AttributeName.Label, ElementDescriptions.OnError_Label, MSBuildSyntaxKind.OnError_Label, MSBuildValueKind.Label),
			];

			UsingTask.attributes = [
				new (UsingTask, AttributeName.Condition, ElementDescriptions.UsingTask_Condition, MSBuildSyntaxKind.UsingTask_Condition, MSBuildValueKind.Condition, helpUrl : HelpUrls.Attribute_Condition),
				new (UsingTask, AttributeName.AssemblyName, ElementDescriptions.UsingTask_AssemblyName, MSBuildSyntaxKind.UsingTask_AssemblyName, MSBuildValueKind.TaskAssemblyName),
				new (UsingTask, AttributeName.AssemblyFile, ElementDescriptions.UsingTask_AssemblyFile, MSBuildSyntaxKind.UsingTask_AssemblyFile, MSBuildValueKind.TaskAssemblyFile),
				new (UsingTask, AttributeName.TaskName, ElementDescriptions.UsingTask_TaskName, MSBuildSyntaxKind.UsingTask_TaskName, MSBuildValueKind.TaskName, required: true),
				new (UsingTask, AttributeName.TaskFactory, ElementDescriptions.UsingTask_TaskFactory, MSBuildSyntaxKind.UsingTask_TaskFactory, MSBuildValueKind.TaskFactory),
				new (UsingTask, AttributeName.Architecture, ElementDescriptions.UsingTask_Architecture, MSBuildSyntaxKind.UsingTask_Architecture, MSBuildValueKind.TaskArchitecture),
				new (UsingTask, AttributeName.Runtime, ElementDescriptions.UsingTask_Runtime, MSBuildSyntaxKind.UsingTask_Runtime, MSBuildValueKind.TaskRuntime),
			];

			TaskBody.attributes = [
				new (TaskBody, AttributeName.Evaluate, ElementDescriptions.UsingTaskBody_Evaluate, MSBuildSyntaxKind.UsingTaskBody_Evaluate, MSBuildValueKind.Bool.AsLiteral ()),
			];

			Output.attributes = [
				new (Output, AttributeName.TaskParameter, ElementDescriptions.Output_TaskParameter, MSBuildSyntaxKind.Output_TaskParameter, MSBuildValueKind.TaskOutputParameterName.AsLiteral (), required : true),
				new (Output, AttributeName.Condition, ElementDescriptions.Output_Condition, MSBuildSyntaxKind.Output_Condition,  MSBuildValueKind.Condition, helpUrl : HelpUrls.Attribute_Condition),
				new (Output, AttributeName.ItemName, ElementDescriptions.Output_ItemName, MSBuildSyntaxKind.Output_ItemName, MSBuildValueKind.ItemName.AsLiteral ()),
				new (Output, AttributeName.PropertyName, ElementDescriptions.Output_PropertyName, MSBuildSyntaxKind.Output_PropertyName, MSBuildValueKind.PropertyName.AsLiteral ()),
			];

			var taskParameterAtt = new MSBuildAttributeSyntax (Task, ABSTRACT, ElementDescriptions.Task_Parameter, MSBuildSyntaxKind.Task_Parameter, MSBuildValueKind.Unknown, abstractKind: MSBuildSyntaxKind.Parameter);
			Task.AbstractAttribute = taskParameterAtt;

			Task.attributes = [
				new (Task, AttributeName.Condition, ElementDescriptions.Task_Condition, MSBuildSyntaxKind.Task_Condition, MSBuildValueKind.Condition, helpUrl : HelpUrls.Attribute_Condition),
				new (Task, AttributeName.ContinueOnError, ElementDescriptions.Task_ContinueOnError, MSBuildSyntaxKind.Task_ContinueOnError, MSBuildValueKind.ContinueOnError),
				new (Task, AttributeName.Architecture, ElementDescriptions.Task_Architecture, MSBuildSyntaxKind.Task_Architecture, MSBuildValueKind.TaskArchitecture),
				new (Task, AttributeName.Runtime, ElementDescriptions.Task_Runtime, MSBuildSyntaxKind.Task_Runtime, MSBuildValueKind.TaskRuntime),
				taskParameterAtt
			];

			Metadata.attributes = [
				new (Metadata, AttributeName.Condition, ElementDescriptions.Metadata_Condition, MSBuildSyntaxKind.Metadata_Condition, MSBuildValueKind.Condition, helpUrl: HelpUrls.Attribute_Condition),
			];
		}
	}
}
