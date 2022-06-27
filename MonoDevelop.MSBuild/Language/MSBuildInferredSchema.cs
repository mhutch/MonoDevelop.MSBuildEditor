// Copyright (c) 2014 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using MonoDevelop.MSBuild.Dom;
using MonoDevelop.MSBuild.Evaluation;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Language.Syntax;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.MSBuild.Language.Typesystem;

namespace MonoDevelop.MSBuild.Language
{
	class MSBuildInferredSchema : IMSBuildSchema
	{
		//FIXME: this means we can't re-use the inferred schema from other toplevels
		readonly bool isToplevel;

		MSBuildInferredSchema (string filename, bool isToplevel)
		{
			this.isToplevel = isToplevel;
			Filename = filename;
		}

		public string Filename { get; }

		public Dictionary<string, PropertyInfo> Properties { get; } = new Dictionary<string, PropertyInfo> (StringComparer.OrdinalIgnoreCase);
		public Dictionary<string, ItemInfo> Items { get; } = new Dictionary<string, ItemInfo> (StringComparer.OrdinalIgnoreCase);
		public Dictionary<string, TaskInfo> Tasks { get; } = new Dictionary<string, TaskInfo> (StringComparer.OrdinalIgnoreCase);
		public Dictionary<string, TargetInfo> Targets { get; } = new Dictionary<string, TargetInfo> (StringComparer.OrdinalIgnoreCase);

		public HashSet<string> Configurations { get; } = new HashSet<string> ();
		public HashSet<string> Platforms { get; } = new HashSet<string> ();

		public bool IsPrivate (string name)
		{
			//properties and items are always visible from files they're used in
			return !isToplevel && name[0] == '_';
		}

		public bool ContainsInfo (ISymbol info) => info switch {
			PropertyInfo _ => Properties.ContainsKey (info.Name),
			ItemInfo _ => Items.ContainsKey (info.Name),
			TaskInfo _ => Tasks.ContainsKey (info.Name),
			TargetInfo _ => Targets.ContainsKey (info.Name),
			_ => false
		};

		public Dictionary<string, ReferenceUsage> ItemUsage { get; }
			= new Dictionary<string, ReferenceUsage> (StringComparer.OrdinalIgnoreCase);
		public Dictionary<string, ReferenceUsage> PropertyUsage { get; }
			= new Dictionary<string, ReferenceUsage> (StringComparer.OrdinalIgnoreCase);
		public Dictionary<(string, string), ReferenceUsage> MetadataUsage { get; }
			= new Dictionary<(string, string), ReferenceUsage> (new MetadataTupleComparer ());

		class MetadataTupleComparer : IEqualityComparer<(string itemName, string name)>
		{
			public bool Equals ((string itemName, string name) x, (string itemName, string name) y)
				=> StringComparer.OrdinalIgnoreCase.Equals (x.itemName, y.itemName)
				&& StringComparer.OrdinalIgnoreCase.Equals (x.name, y.name);

			public int GetHashCode ((string itemName, string name) obj)
				=> StringComparer.OrdinalIgnoreCase.GetHashCode (obj.itemName)
				^ StringComparer.OrdinalIgnoreCase.GetHashCode (obj.name);
		}

		public static MSBuildInferredSchema Build (MSBuildProjectElement project, string filename, bool isToplevel, MSBuildParserContext parseContext)
		{
			var schema = new MSBuildInferredSchema (filename, isToplevel);
			try {
				schema.Build (project, parseContext);
			} catch (Exception ex) {
				LoggingService.LogError ($"Error in schema inference for {filename}", ex);
			}
			return schema;
		}

		void Build (MSBuildElement element, MSBuildParserContext parseContext)
		{
			switch (element.SyntaxKind) {
			case MSBuildSyntaxKind.Item:
				CollectItem (element.ElementName, ReferenceUsage.Write);
				break;
			case MSBuildSyntaxKind.Property:
				CollectProperty (element.ElementName, ReferenceUsage.Write);
				break;
			case MSBuildSyntaxKind.UsingTask:
				CollectTaskDefinition (element, parseContext);
				break;
			case MSBuildSyntaxKind.Task:
				CollectTask (element.ElementName);
				break;
			case MSBuildSyntaxKind.Target:
				var target = (MSBuildTargetElement)element;
				var targetName = target.NameAttribute?.AsConstString ();
				if (!string.IsNullOrEmpty (targetName)) {
					CollectTarget (targetName);
				}
				break;
			case MSBuildSyntaxKind.Parameter:
				var taskName = ((MSBuildUsingTaskElement)element.Parent.Parent).TaskNameAttribute?.Value;
				if (taskName is ExpressionText t && !string.IsNullOrEmpty (t.Value)) {
					CollectTaskParameterDefinition (t.Value, (MSBuildParameterElement)element);
				}
				break;
			case MSBuildSyntaxKind.Metadata:
				CollectMetadata (element.Parent.ElementName, element.ElementName, ReferenceUsage.Write);
				break;
			}

			if (element.Value != null) {
				ExtractReferences (GetElementKind (element), element.Value);
			}

			foreach (var att in element.Attributes) {
				if (att.Value != null ) {
					MSBuildValueKind attKind = GetAttributeKind (element, att);
					ExtractReferences (attKind, att.Value);
				}
				if (att.SyntaxKind == MSBuildSyntaxKind.Item_Metadata) {
					CollectMetadata (element.ElementName, att.Name, ReferenceUsage.Write);
				}
			}

			foreach (var child in element.Elements) {
				Build (child, parseContext);
			}
		}

		static MSBuildValueKind GetAttributeKind (MSBuildElement element, MSBuildAttribute att)
		{
			var attKind = att.Syntax.ValueKind;

			if (element.SyntaxKind == MSBuildSyntaxKind.Item && element.IsElementNamed ("ProjectConfiguration")) {
				if (att.IsNamed ("Configuration")) {
					attKind = MSBuildValueKind.Configuration;
				} else if (att.IsNamed ("Platform")) {
					attKind = MSBuildValueKind.Platform;
				}
			}

			return attKind;
		}

		static MSBuildValueKind GetElementKind (MSBuildElement element)
		{
			var kind = element.Syntax.ValueKind;

			if (element.SyntaxKind == MSBuildSyntaxKind.Property) {
				switch (element.ElementName.ToLowerInvariant ()) {
				case "configuration":
					return MSBuildValueKind.Configuration;
				case "configurations":
					return MSBuildValueKind.Configuration.AsList ();
				case "platform":
					return MSBuildValueKind.Platform;
				case "platforms":
					return MSBuildValueKind.Platform.AsList ();
				}
			} else if (element.SyntaxKind == MSBuildSyntaxKind.Metadata && (element.Parent?.IsElementNamed ("ProjectConfiguration") ?? false)) {
				if (element.IsElementNamed ("Configuration")) {
					return MSBuildValueKind.Configuration;
				} else if (element.IsElementNamed ("Platform")) {
					return MSBuildValueKind.Platform;
				}
			}

			return kind;
		}

		void CollectItem (string itemName, ReferenceUsage usage)
		{
			if (ItemUsage.TryGetValue (itemName, out var existingUsage)) {
				if (existingUsage == usage) {
					return;
				}
				usage |= existingUsage;
			} else {
				Items.Add (itemName, new ItemInfo (itemName, null));
			}
			ItemUsage[itemName] = usage;
		}

		void CollectProperty (string propertyName, ReferenceUsage usage)
		{
			if (PropertyUsage.TryGetValue (propertyName, out var existingUsage)) {
				if (existingUsage == usage) {
					return;
				}
				usage |= existingUsage;
			} else if (!Builtins.Properties.ContainsKey (propertyName)) {
				Properties.Add (propertyName, new PropertyInfo (propertyName, null));
			}
			PropertyUsage[propertyName] = usage;
		}

		void CollectTarget (string name)
		{
			if (name != null && !Targets.ContainsKey (name)) {
				Targets[name] = new TargetInfo (name, null);
			}
		}

		void CollectMetadata (string itemName, string metadataName, ReferenceUsage usage)
		{
			if (string.IsNullOrEmpty (metadataName)) {
				throw new ArgumentException ($"'{nameof (metadataName)}' cannot be null or empty.", nameof (metadataName));
			}
			if (itemName == null) {
				return;
			}
			CollectItem (itemName, usage);
			if (MetadataUsage.TryGetValue ((itemName, metadataName), out var existingUsage)) {
				if (existingUsage == usage) {
					return;
				}
				usage |= existingUsage;
			} else if (!Builtins.Metadata.ContainsKey (metadataName)) {
				var item = Items[itemName];
				item.Metadata.Add (metadataName, new MetadataInfo (metadataName, null, item: item));
			}
			MetadataUsage[(itemName, metadataName)] = usage;
		}

		void CollectTask (string name)
		{
			if (!Tasks.TryGetValue (name, out TaskInfo task)) {
				Tasks[name] = task = new TaskInfo (name, null, null, null, null, null, 0);
			}
		}

		void CollectTaskParameter (string taskName, string parameterName, bool isOutput)
		{
			var task = Tasks[taskName];
			if (task.IsInferred && !task.ForceInferAttributes) {
				return;
			}
			if (task.Parameters.TryGetValue (parameterName, out TaskParameterInfo pi)) {
				if (pi.IsOutput || !isOutput) {
					return;
				}
			}
			task.Parameters[parameterName] = new TaskParameterInfo (parameterName, null, false, isOutput, MSBuildValueKind.Unknown);
		}

		void CollectTaskParameterDefinition (string taskName, MSBuildParameterElement def)
		{
			var task = Tasks[taskName];
			var parameterName = def.ElementName;
			if (task.Parameters.ContainsKey (parameterName)) {
				return;
			}

			bool isRequired = def.RequiredAttribute?.AsConstBool () ?? false;
			bool isOutout = def.OutputAttribute?.AsConstBool () ?? false;

			var kind = MSBuildValueKind.Unknown;
			bool isList = false;

			var type = def.ParameterTypeAttribute?.AsConstString ();
			if (type != null) {
				if (type.EndsWith ("[]", StringComparison.Ordinal)) {
					type = type.Substring (0, type.Length - 2);
					isList = true;
				}

				switch (type.ToLowerInvariant ()) {
				case "system.int32":
				case "system.uint32":
				case "system.int64":
				case "system.uint64":
					kind = MSBuildValueKind.Int;
					break;
				case "system.boolean":
					kind = MSBuildValueKind.Bool;
					break;
				case "system.string":
					kind = MSBuildValueKind.String;
					break;
				case "microsoft.build.framework.itaskitem":
					kind = MSBuildValueKind.UnknownItem;
					break;
				}
			}

			if (isList) {
				kind = kind.AsList ();
			}

			task.Parameters.Add (parameterName, new TaskParameterInfo (parameterName, null, isRequired, isOutout, kind));
		}

		void CollectTaskDefinition (MSBuildElement element, MSBuildParserContext parseContext)
		{
			string taskName = null, assemblyName = null, assemblyFileStr = null, taskFactory = null;
			ExpressionNode assemblyFile = null;
			foreach (var att in element.Attributes) {
				switch (att.SyntaxKind) {
				case MSBuildSyntaxKind.UsingTask_AssemblyFile:
					assemblyFile = att?.Value;
					assemblyFileStr = att?.XAttribute?.Value;
					break;
				case MSBuildSyntaxKind.UsingTask_AssemblyName:
					assemblyName = att?.AsConstString ();
					break;
				case MSBuildSyntaxKind.UsingTask_TaskFactory:
					taskFactory = att?.AsConstString ();
					break;
				case MSBuildSyntaxKind.UsingTask_TaskName:
					taskName = att?.AsConstString ();
					break;
				}
			}

			if (taskName == null) {
				return;
			}

			int nameIdx = taskName.LastIndexOf ('.');
			string name = taskName.Substring (nameIdx + 1);
			if (string.IsNullOrEmpty (name)) {
				return;
			}

			if (taskFactory == null && (assemblyName != null || assemblyFile != null)) {
				//FIXME create this lazily and cache it
				var evalCtx = new MSBuildCollectedValuesEvaluationContext (new MSBuildFileEvaluationContext (parseContext.RuntimeEvaluationContext, parseContext.ProjectPath, Filename), parseContext.PropertyCollector);
				TaskInfo info = parseContext.TaskBuilder.CreateTaskInfo (taskName, assemblyName, assemblyFile, assemblyFileStr, Filename, element.XElement.Span.Start, evalCtx);
				if (info != null) {
					Tasks[info.Name] = info;
					return;
				}
			}

			//HACK: RoslynCodeTaskFactory determines the parameters automatically from the code, until we
			//can do this too we need to force inference
			bool forceInferAttributes = taskFactory != null && (
				string.Equals (taskFactory, "RoslynCodeTaskFactory", StringComparison.OrdinalIgnoreCase) || (
					string.Equals (taskFactory, "CodeTaskFactory", StringComparison.OrdinalIgnoreCase) &&
					string.Equals ((assemblyFile as ExpressionText)?.Value, "$(RoslynCodeTaskFactory)", StringComparison.OrdinalIgnoreCase
				)) &&
				(element.GetAttribute (MSBuildSyntaxKind.ParameterGroup) == null));

			Tasks[name] = new TaskInfo (name, null, null, null, null, Filename, element.XElement.Span.Start) {
				ForceInferAttributes = forceInferAttributes
			};
		}

		void ExtractReferences (MSBuildValueKind kind, ExpressionNode expression)
		{
			foreach (var node in expression.WithAllDescendants ()) {
				switch (node) {
				case ExpressionPropertyName prop:
					CollectProperty (prop.Name, ReferenceUsage.Read);
					break;
				case ExpressionItemName item:
					CollectItem (item.Name, ReferenceUsage.Read);
					break;
				case ExpressionMetadata meta:
					var itemName = meta.GetItemName ();
					if (itemName != null) {
						CollectMetadata (itemName, meta.MetadataName, ReferenceUsage.Read);
					}
					break;
				case ExpressionText literal:
					if (literal.IsPure) {
						var value = literal.GetUnescapedValue ().Trim ();
						switch (kind.WithoutModifiers ()) {
						case MSBuildValueKind.ItemName:
							CollectItem (value, ReferenceUsage.Unknown);
							break;
						case MSBuildValueKind.TargetName:
							CollectTarget (value);
							break;
						case MSBuildValueKind.PropertyName:
							CollectProperty (value, ReferenceUsage.Unknown);
							break;
						case MSBuildValueKind.Configuration:
							Configurations.Add (value);
							break;
						case MSBuildValueKind.Platform:
							Platforms.Add (value);
							break;
						}
					}
					break;
				}
			}

			if (kind == MSBuildValueKind.Condition) {
				CollectComparisonProperties (expression);
			}
		}

		void CollectComparisonProperties (ExpressionNode expression)
		{
			if (!(expression is ExpressionConditionOperator op
				&& (op.OperatorKind == ExpressionOperatorKind.Equal || op.OperatorKind == ExpressionOperatorKind.NotEqual)
				&& op.Right is QuotedExpression quot
				&& quot.Expression is ExpressionText txt
				)
			) {
				return;
			}

			var left = op.Left;
			if (left is QuotedExpression qtCat) {
				left = qtCat.Expression;
			}

			// '$(Configuration)'=='Debug')
			if (left is ExpressionProperty prop && prop.IsSimpleProperty) {
				CollectComparisonProperty (prop, txt.Value);
				return;
			}

			// '$(Configuration)|$(Platform)'=='Debug|AnyCPU')
			if (
				left is ConcatExpression concat
				&& concat.Nodes.Count == 3
				&& concat.Nodes[1] is ExpressionText t && t.Value == "|"
				&& concat.Nodes[0] is ExpressionProperty p1 && p1.IsSimpleProperty
				&& concat.Nodes[2] is ExpressionProperty p2 && p2.IsSimpleProperty
			) {
				var s = txt.Value.Split ('|');
				if (s.Length == 2) {
					CollectComparisonProperty (p1, s[0]);
					CollectComparisonProperty (p2, s[1]);
				}
			}
		}

		void CollectComparisonProperty (ExpressionProperty prop, string value)
		{
			if (string.Equals (prop.Name, "Configuration", StringComparison.OrdinalIgnoreCase)) {
				Configurations.Add (value);
			} else if (string.Equals (prop.Name, "Platform", StringComparison.OrdinalIgnoreCase)) {
				Platforms.Add (value);
			}
		}
	}
}