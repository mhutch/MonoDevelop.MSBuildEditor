// Copyright (c) 2014 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;

using MonoDevelop.MSBuild.Dom;
using MonoDevelop.MSBuild.Evaluation;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Language.Syntax;
using MonoDevelop.MSBuild.Language.Typesystem;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Logging;

namespace MonoDevelop.MSBuild.Language
{
	partial class MSBuildInferredSchema : IMSBuildSchema
	{
		//FIXME: this means we can't re-use the inferred schema from other toplevels
		readonly bool isToplevel;

		MSBuildInferredSchema (string? filename, bool isToplevel)
		{
			this.isToplevel = isToplevel;
			Filename = filename;
		}

		/// <summary>
		/// The filename for which this is a schema. May be null if the file has not been saved.
		/// </summary>
		public string? Filename { get; }

		public Dictionary<string, PropertyInfo> Properties { get; } = new Dictionary<string, PropertyInfo> (StringComparer.OrdinalIgnoreCase);
		public Dictionary<string, ItemInfo> Items { get; } = new Dictionary<string, ItemInfo> (StringComparer.OrdinalIgnoreCase);
		public Dictionary<string, TaskInfo> Tasks { get; } = new Dictionary<string, TaskInfo> (StringComparer.OrdinalIgnoreCase);
		public Dictionary<string, TargetInfo> Targets { get; } = new Dictionary<string, TargetInfo> (StringComparer.OrdinalIgnoreCase);
		public Dictionary<string, CustomTypeInfo> Types { get; } = new Dictionary<string, CustomTypeInfo> (StringComparer.OrdinalIgnoreCase);

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
			MetadataInfo m => MetadataUsage.ContainsKey((m.Item?.Name, m.Name)),
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

		public static MSBuildInferredSchema Build (MSBuildProjectElement project, string? filename, bool isToplevel, MSBuildParserContext parseContext)
		{
			var schema = new MSBuildInferredSchema (filename, isToplevel);
			try {
				schema.Build (project, parseContext);
			} catch (Exception ex) {
				LogInternalError (parseContext.Logger, filename, ex);
			}
			return schema;
		}

		void Build (MSBuildElement element, MSBuildParserContext parseContext)
		{
			switch (element.SyntaxKind) {
			case MSBuildSyntaxKind.Item:
				CollectItem (element.Name, ReferenceUsage.Write);
				goto default;
			case MSBuildSyntaxKind.Property:
				CollectProperty (element.Name, ReferenceUsage.Write);
				goto default;
			case MSBuildSyntaxKind.UsingTask:
				CollectTaskDefinition ((MSBuildUsingTaskElement)element, parseContext);
				goto default;
			case MSBuildSyntaxKind.Task:
				CollectTask (element.Name);
				goto default;
			case MSBuildSyntaxKind.Target:
				var target = (MSBuildTargetElement)element;
				var targetName = target.NameAttribute?.AsConstString ();
				if (!string.IsNullOrEmpty (targetName)) {
					CollectTarget (targetName);
				}
				goto default;
			case MSBuildSyntaxKind.Parameter:
				var taskName = ((MSBuildUsingTaskElement)element.Parent.Parent).TaskNameAttribute?.Value;
				if (taskName is ExpressionText t && !string.IsNullOrEmpty (t.Value)) {
					CollectTaskParameterDefinition (t.Value, (MSBuildParameterElement)element);
				}
				goto default;
			case MSBuildSyntaxKind.Metadata:
				// <ProjectReference><OutputItemType>
				if (element.IsElementNamed("OutputItemType") && element.Parent.IsElementNamed("ProjectReference") && element.Value.AsConstString() is string outputItemType) {
					CollectItem (outputItemType, ReferenceUsage.Write);
					// skip default, we know it's const and we collected it
					break;
				}
				CollectMetadata (element.Parent.Name, element.Name, ReferenceUsage.Write);
				goto default;
			default:
				if (element.Value != null) {
					ExtractReferences (GetElementKind (element), element.Value);
				}
				break;
			}


			foreach (var att in element.Attributes) {
				switch (att.SyntaxKind) {
				case MSBuildSyntaxKind.Item_Metadata:
					// <ProjectReference OutputItemType=""
					if (element.IsElementNamed ("ProjectReference") && att.IsNamed ("OutputItemType") && att.AsConstString () is string outputItemType) {
						CollectItem (outputItemType, ReferenceUsage.Write);
						break;
					}
					CollectMetadata (element.Name, att.Name, ReferenceUsage.Write);
					goto default;
				case MSBuildSyntaxKind.Output_ItemName:
					// <TaskName><Output ItemName=""
					if (att.AsConstString () is string itemName) {
						CollectItem (itemName, ReferenceUsage.Write);
						break;
					}
					goto default;
				case MSBuildSyntaxKind.Output_PropertyName:
					// <TaskName><Output PropertyName=""
					if (att.AsConstString () is string propertyName) {
						CollectProperty (propertyName, ReferenceUsage.Write);
						break;
					}
					goto default;
				default:
					if (att.Value is not null) {
						MSBuildValueKind attKind = GetAttributeKind (element, att);
						ExtractReferences (attKind, att.Value);
					}
					break;
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
				switch (element.Name.ToLowerInvariant ()) {
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
				var kind = MSBuildIdentifier.InferValueKind (itemName, MSBuildSyntaxKind.Item);
				Items.Add (itemName, new InferredItemInfo (itemName, kind));
			}
			ItemUsage[itemName] = usage;
		}

		class InferredItemInfo (string name, MSBuildValueKind inferredKind) : ItemInfo (name, null, valueKind: inferredKind), IInferredSymbol
		{
		}

		void CollectProperty (string propertyName, ReferenceUsage usage)
		{
			if (PropertyUsage.TryGetValue (propertyName, out var existingUsage)) {
				if (existingUsage == usage) {
					return;
				}
				usage |= existingUsage;
			} else if (!MSBuildIntrinsics.Properties.ContainsKey (propertyName)) {
				var kind = MSBuildIdentifier.InferValueKind (propertyName, MSBuildSyntaxKind.Property);
				Properties.Add (propertyName, new InferredPropertyInfo (propertyName, kind));
			}
			PropertyUsage[propertyName] = usage;
		}

		class InferredPropertyInfo (string name, MSBuildValueKind inferredKind) : PropertyInfo(name, null, inferredKind), IInferredSymbol
		{
		}

		void CollectTarget (string name)
		{
			if (name != null && !Targets.ContainsKey (name)) {
				Targets[name] = new InferredTargetInfo (name);
			}
		}

		class InferredTargetInfo (string name) : TargetInfo (name, null), IInferredSymbol
		{
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
			} else if (!MSBuildIntrinsics.Metadata.ContainsKey (metadataName)) {
				var item = Items[itemName];
				var kind = MSBuildIdentifier.InferValueKind (metadataName, MSBuildSyntaxKind.Metadata);
				item.Metadata.Add (metadataName, new InferredMetadataInfo (metadataName, kind, item: item));
			}
			MetadataUsage[(itemName, metadataName)] = usage;
		}

		class InferredMetadataInfo (string name, MSBuildValueKind inferredKind, ItemInfo item) : MetadataInfo (name, null, valueKind: inferredKind, item: item), IInferredSymbol
		{
		}

		void CollectTask (string name)
		{
			if (!Tasks.TryGetValue (name, out TaskInfo task)) {
				Tasks[name] = new InferredTaskInfo (name);
			}
		}

		class InferredTaskInfo (string name) : TaskInfo (name, null, TaskDeclarationKind.Inferred, null, null, null, null, null, null) { }

		void CollectTaskParameter (string taskName, string parameterName, bool isOutput)
		{
			var task = Tasks[taskName];
			if (task.DeclarationKind == TaskDeclarationKind.Inferred || task.DeclarationKind == TaskDeclarationKind.TaskFactoryImplicitParameters) {
				return;
			}
			if (task.Parameters.TryGetValue (parameterName, out TaskParameterInfo pi)) {
				if (pi.IsOutput || !isOutput) {
					return;
				}
			}
			task.SetParameter(new InferredTaskParameter (parameterName, false, isOutput, MSBuildValueKind.Unknown));
		}

		class InferredTaskParameter (string parameterName, bool isRequired, bool isOutput, MSBuildValueKind kind)
			: TaskParameterInfo (parameterName, null, isRequired, isOutput, kind)
		{
		}

		void CollectTaskParameterDefinition (string taskName, MSBuildParameterElement def)
		{
			var task = Tasks[taskName];
			var parameterName = def.Name;
			if (task.Parameters.ContainsKey (parameterName)) {
				return;
			}

			bool isRequired = def.RequiredAttribute?.AsConstBool () ?? false;
			bool isOutput = def.OutputAttribute?.AsConstBool () ?? false;

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

			task.SetParameter (new InferredTaskParameter (parameterName, isRequired, isOutput, kind));
		}

		void CollectTaskDefinition (MSBuildUsingTaskElement element, MSBuildParserContext parseContext)
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

			var fullTaskName = taskName;
			if (!TaskInfo.ValidateTaskName(fullTaskName, out taskName, out string taskNamespace)) {
				return;
			}

			if (taskFactory == null && (assemblyName != null || assemblyFile != null)) {
				//FIXME create this lazily and cache it
				var evalCtx = new MSBuildCollectedValuesEvaluationContext (
					MSBuildFileEvaluationContext.Create (parseContext.ProjectEvaluationContext, parseContext.Logger, Filename),
					parseContext.PropertyCollector
				);

				TaskInfo info = parseContext.TaskBuilder.CreateTaskInfo (fullTaskName, assemblyName, assemblyFile, assemblyFileStr, Filename, element.XElement.Span, evalCtx, parseContext.Logger);

				if (info != null) {
					Tasks[info.Name] = info;
					return;
				} else {
					// created placeholder task marked as unresolved for analyzers etc
					Tasks[taskName] = new TaskInfo (taskName, null, TaskDeclarationKind.AssemblyUnresolved, fullTaskName, assemblyName, assemblyFileStr, Filename, element.XElement.Span, null);
					return;
				}
			}

			// HACK: some factories such as RoslynCodeTaskFactory determine the parameters automatically from the code
			// but we cannot do that, so we mark the task as TaskDeclarationKind.TaskFactoryNoParameters which force inferences of the parameters from usage
			//can do this too we need to force inference

			var taskParameters = new Dictionary<string, TaskParameterInfo> (StringComparer.OrdinalIgnoreCase);
			TaskDeclarationKind declarationKind = TaskDeclarationKind.TaskFactoryImplicitParameters;

			if (element.ParameterGroup is MSBuildParameterGroupElement parameterGroup) {
				declarationKind = TaskDeclarationKind.TaskFactoryExplicitParameters;
				foreach (var parameterElement in parameterGroup.ParameterElements) {
					var parameter = new TaskParameterInfo (
						parameterElement.Name,
						null,
						parameterElement.RequiredAttribute?.AsConstBool () ?? false,
						parameterElement.OutputAttribute?.AsConstBool () ?? false,
						ValueKindExtensions.FromFullTypeName (parameterElement.ParameterTypeAttribute?.AsConstString ()));
					taskParameters[parameter.Name] = parameter;
				}
			}

			Tasks[taskName] = new TaskInfo (taskName, null, declarationKind, fullTaskName, null, null, Filename, element.XElement.Span, null, taskParameters);
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
						var value = literal.GetUnescapedValue (true, out _, out _).Trim ();
						if (value.Length == 0) {
							continue;
						}
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
				var value = txt.GetUnescapedValue (true, out _, out _);
				CollectComparisonProperty (prop, value);
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
				var value = txt.GetUnescapedValue (true, out _, out _);
				var split = value.Split ('|');
				if (split.Length == 2) {
					CollectComparisonProperty (p1, split[0]);
					CollectComparisonProperty (p2, split[1]);
				}
			}
		}

		void CollectComparisonProperty (ExpressionProperty prop, string value)
		{
			value = value.Trim ();
			if (value.Length == 0) {
				return;
			}
			if (string.Equals (prop.Name, "Configuration", StringComparison.OrdinalIgnoreCase)) {
				Configurations.Add (value);
			} else if (string.Equals (prop.Name, "Platform", StringComparison.OrdinalIgnoreCase)) {
				Platforms.Add (value);
			}
		}

		public static MSBuildValueKind InferValueKindFromName (ISymbol symbol)
			=> symbol switch {
				ItemInfo item => MSBuildValueKind.FileOrFolder.AsList (),
				MetadataInfo metadata => MSBuildIdentifier.InferValueKind (metadata.Name, MSBuildSyntaxKind.Metadata),
				PropertyInfo property => MSBuildIdentifier.InferValueKind (property.Name, MSBuildSyntaxKind.Property),
				_ => MSBuildValueKind.Unknown
			};


		[LoggerMessage (EventId = 0, Level = LogLevel.Error, Message = "Internal error in schema inference for {filename}")]
		static partial void LogInternalError (ILogger logger, UserIdentifiableFileName filename, Exception ex);
	}
}