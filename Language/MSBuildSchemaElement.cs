// Copyright (c) 2014 Xamarin Inc.
// Copyright (c) Microsoft. ALl rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace MonoDevelop.MSBuildEditor.Language
{
	class MSBuildSchemaElement
	{
		static readonly string[] emptyArray = new string[0];

		string[] children, attributes;

		public IEnumerable<string> Children { get { return children; } }
		public IEnumerable<string> Attributes { get { return attributes; } }
		public MSBuildKind Kind { get; private set; }
		public MSBuildKind? ChildType { get; private set; }
		public bool IsSpecial  { get; private set; }

		public MSBuildSchemaElement (
			MSBuildKind kind, bool isSpecial = false, string[] children = null,
			string[] attributes = null, MSBuildKind? childType = null)
		{
			this.Kind = kind;
			this.children = children ?? emptyArray;
			this.attributes = attributes ?? emptyArray;
			this.ChildType = childType;
			this.IsSpecial = isSpecial;
		}

		public bool HasChild (string name)
		{
			return children != null && children.Contains (name, StringComparer.OrdinalIgnoreCase);
		}

		public static MSBuildSchemaElement Get (string name, MSBuildSchemaElement parent = null)
		{
			//if not in parent's known children, and parent has special children, then it's a special child
			if (parent != null && parent.ChildType != null && !parent.HasChild (name))
				name = parent.ChildType.ToString ();
			builtin.TryGetValue (name, out MSBuildSchemaElement result);
			return result;
		}

		static readonly Dictionary<string, MSBuildSchemaElement> builtin = new Dictionary<string, MSBuildSchemaElement> (StringComparer.OrdinalIgnoreCase) {
			{
				"Choose", new MSBuildSchemaElement (MSBuildKind.Choose,
					children: new[] { "Otherwise", "When" }
				)
			},
			{
				"Import", new MSBuildSchemaElement (MSBuildKind.Import,
					attributes: new[] { "Condition", "Project", "Sdk" }
				)
			},
			{
				"ImportGroup", new MSBuildSchemaElement (MSBuildKind.ImportGroup,
					children: new[] { "Import" },
					attributes: new[] { "Condition" }
				)
			},
			{
				"Item", new MSBuildSchemaElement (MSBuildKind.Item,
					childType: MSBuildKind.ItemMetadata,
					attributes: new[] { "Condition", "Exclude", "Include", "Remove", "Update" },
					isSpecial: true
				)
			},
			{
				"ItemDefinitionGroup", new MSBuildSchemaElement (MSBuildKind.ItemDefinitionGroup,
					childType: MSBuildKind.Item,
					attributes: new[] { "Condition" }
				)
			},
			{
				"ItemGroup", new MSBuildSchemaElement (MSBuildKind.ItemGroup,
					childType: MSBuildKind.Item,
					attributes: new[] { "Condition" }
				)
			},
			{
				"ItemMetadata", new MSBuildSchemaElement (MSBuildKind.ItemMetadata,
					attributes: new[] { "Condition" },
					childType: MSBuildKind.Expression,
					isSpecial: true
				)
			},
			{
				"OnError", new MSBuildSchemaElement (MSBuildKind.OnError,
					attributes: new[] { "Condition", "ExecuteTargets" }
				)
			},
			{
				"Otherwise", new MSBuildSchemaElement (MSBuildKind.Otherwise,
					children: new[] { "Choose", "ItemGroup", "PropertyGroup" }
				)
			},
			{
				"Output", new MSBuildSchemaElement (MSBuildKind.Output,
					attributes: new[] { "Condition", "ItemName", "PropertyName", "TaskParameter" }
				)
			},
			{
				"Parameter", new MSBuildSchemaElement (MSBuildKind.Parameter,
					attributes: new[] { "Output", "ParameterType", "Required" },
					isSpecial: true
				)
			},
			{
				"ParameterGroup", new MSBuildSchemaElement (MSBuildKind.ParameterGroup,
					childType: MSBuildKind.Parameter
				)
			},
			{
				"Project", new MSBuildSchemaElement (MSBuildKind.Project,
					children: new[] {
						"Choose", "Import", "ItemGroup", "ProjectExtensions", "PropertyGroup", "Target", "UsingTask"
					},
					attributes: new[] {
						"DefaultTargets", "InitialTargets", "ToolsVersion", "TreatAsLocalProperty", "xmlns", "Sdk"
					}
				)
			},
			{
				"ProjectExtensions", new MSBuildSchemaElement (MSBuildKind.ProjectExtensions,
					childType: MSBuildKind.Data
				)
			},
			{
				"Property", new MSBuildSchemaElement (MSBuildKind.Property,
					attributes: new[] { "Condition" },
					childType: MSBuildKind.Expression,
					isSpecial: true
				)
			},
			{
				"PropertyGroup", new MSBuildSchemaElement (MSBuildKind.PropertyGroup,
					childType: MSBuildKind.Property,
					attributes: new[] { "Condition" }
				)
			},
			{
				"Target", new MSBuildSchemaElement (MSBuildKind.Target,
					childType: MSBuildKind.Task,
					children: new[] { "OnError", "ItemGroup", "PropertyGroup" },
					attributes: new[] {
						"AfterTargets", "BeforeTargets", "Condition", "DependsOnTargets", "Inputs",
						"KeepDuplicateOutputs", "Name", "Outputs", "Returns"
					}
				)
			},
			{
				"Task", new MSBuildSchemaElement (MSBuildKind.Task,
					children: new[] { "Output" },
					attributes: new[] { "Condition", "ContinueOnError", "Parameter" },
					isSpecial: true
				)
			},
			{
				"TaskBody", new MSBuildSchemaElement (MSBuildKind.TaskBody,
					childType: MSBuildKind.Data,
					attributes: new[] { "Evaluate" }
				)
			},
			{
				"UsingTask", new MSBuildSchemaElement (MSBuildKind.UsingTask,
					children: new[] { "ParameterGroup", "TaskBody" },
					attributes: new[] { "AssemblyFile", "AssemblyName", "Condition", "TaskFactory", "TaskName" }
				)
			},
			{
				"When", new MSBuildSchemaElement (MSBuildKind.When,
					children: new[] { "Choose", "ItemGroup", "PropertyGroup" },
					attributes: new[] { "Condition" }
				)
			},
		};
	}

	enum MSBuildKind
	{
		Choose,
		Import,
		ImportGroup,
		Item,
		ItemDefinitionGroup,
		ItemGroup,
		ItemMetadata,
		OnError,
		Otherwise,
		Output,
		Parameter,
		ParameterGroup,
		Project,
		ProjectExtensions,
		Property,
		PropertyGroup,
		Target,
		Task,
		TaskBody,
		UsingTask,
		When,
		Data,
		Expression,
		ItemReference,
		PropertyReference,
		MetadataReference,
		TaskParameter,
	}
}