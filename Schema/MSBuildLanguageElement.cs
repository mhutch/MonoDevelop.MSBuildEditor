// Copyright (c) 2014 Xamarin Inc.
// Copyright (c) Microsoft. ALl rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace MonoDevelop.MSBuildEditor.Language
{
	class MSBuildLanguageElement
	{
		static readonly string[] emptyArray = new string[0];

		string[] children, attributes;

		public IEnumerable<string> Children { get { return children; } }
		public IEnumerable<string> Attributes { get { return attributes; } }
		public MSBuildKind Kind { get; private set; }
		public MSBuildKind? ChildType { get; private set; }
		public bool IsSpecial  { get; private set; }

		public MSBuildLanguageElement (
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

		public static MSBuildLanguageElement Get (string name, MSBuildLanguageElement parent = null)
		{
			//if not in parent's known children, and parent has special children, then it's a special child
			if (parent != null && parent.ChildType != null && !parent.HasChild (name))
				name = parent.ChildType.ToString ();
			builtin.TryGetValue (name, out MSBuildLanguageElement result);
			return result;
		}

		static readonly Dictionary<string, MSBuildLanguageElement> builtin = new Dictionary<string, MSBuildLanguageElement> (StringComparer.OrdinalIgnoreCase) {
			{
				"Choose", new MSBuildLanguageElement (MSBuildKind.Choose,
					children: new[] { "Otherwise", "When" }
				)
			},
			{
				"Import", new MSBuildLanguageElement (MSBuildKind.Import,
					attributes: new[] { "Condition", "Project", "Sdk" }
				)
			},
			{
				"ImportGroup", new MSBuildLanguageElement (MSBuildKind.ImportGroup,
					children: new[] { "Import" },
					attributes: new[] { "Condition" }
				)
			},
			{
				"Item", new MSBuildLanguageElement (MSBuildKind.Item,
					childType: MSBuildKind.ItemMetadata,
					attributes: new[] { "Condition", "Exclude", "Include", "Remove", "Update" },
					isSpecial: true
				)
			},
			{
				"ItemDefinitionGroup", new MSBuildLanguageElement (MSBuildKind.ItemDefinitionGroup,
					childType: MSBuildKind.Item,
					attributes: new[] { "Condition" }
				)
			},
			{
				"ItemGroup", new MSBuildLanguageElement (MSBuildKind.ItemGroup,
					childType: MSBuildKind.Item,
					attributes: new[] { "Condition" }
				)
			},
			{
				"ItemMetadata", new MSBuildLanguageElement (MSBuildKind.ItemMetadata,
					attributes: new[] { "Condition" },
					childType: MSBuildKind.Expression,
					isSpecial: true
				)
			},
			{
				"OnError", new MSBuildLanguageElement (MSBuildKind.OnError,
					attributes: new[] { "Condition", "ExecuteTargets" }
				)
			},
			{
				"Otherwise", new MSBuildLanguageElement (MSBuildKind.Otherwise,
					children: new[] { "Choose", "ItemGroup", "PropertyGroup" }
				)
			},
			{
				"Output", new MSBuildLanguageElement (MSBuildKind.Output,
					attributes: new[] { "Condition", "ItemName", "PropertyName", "TaskParameter" }
				)
			},
			{
				"Parameter", new MSBuildLanguageElement (MSBuildKind.Parameter,
					attributes: new[] { "Output", "ParameterType", "Required" },
					isSpecial: true
				)
			},
			{
				"ParameterGroup", new MSBuildLanguageElement (MSBuildKind.ParameterGroup,
					childType: MSBuildKind.Parameter
				)
			},
			{
				"Project", new MSBuildLanguageElement (MSBuildKind.Project,
					children: new[] {
						"Choose", "Import", "ItemGroup", "ProjectExtensions", "PropertyGroup", "Target", "UsingTask"
					},
					attributes: new[] {
						"DefaultTargets", "InitialTargets", "ToolsVersion", "TreatAsLocalProperty", "xmlns", "Sdk"
					}
				)
			},
			{
				"ProjectExtensions", new MSBuildLanguageElement (MSBuildKind.ProjectExtensions,
					childType: MSBuildKind.Data
				)
			},
			{
				"Property", new MSBuildLanguageElement (MSBuildKind.Property,
					attributes: new[] { "Condition" },
					childType: MSBuildKind.Expression,
					isSpecial: true
				)
			},
			{
				"PropertyGroup", new MSBuildLanguageElement (MSBuildKind.PropertyGroup,
					childType: MSBuildKind.Property,
					attributes: new[] { "Condition" }
				)
			},
			{
				"Target", new MSBuildLanguageElement (MSBuildKind.Target,
					childType: MSBuildKind.Task,
					children: new[] { "OnError", "ItemGroup", "PropertyGroup" },
					attributes: new[] {
						"AfterTargets", "BeforeTargets", "Condition", "DependsOnTargets", "Inputs",
						"KeepDuplicateOutputs", "Name", "Outputs", "Returns"
					}
				)
			},
			{
				"Task", new MSBuildLanguageElement (MSBuildKind.Task,
					children: new[] { "Output" },
					attributes: new[] { "Condition", "ContinueOnError", "Parameter" },
					isSpecial: true
				)
			},
			{
				"TaskBody", new MSBuildLanguageElement (MSBuildKind.TaskBody,
					childType: MSBuildKind.Data,
					attributes: new[] { "Evaluate" }
				)
			},
			{
				"UsingTask", new MSBuildLanguageElement (MSBuildKind.UsingTask,
					children: new[] { "ParameterGroup", "TaskBody" },
					attributes: new[] { "AssemblyFile", "AssemblyName", "Condition", "TaskFactory", "TaskName" }
				)
			},
			{
				"When", new MSBuildLanguageElement (MSBuildKind.When,
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