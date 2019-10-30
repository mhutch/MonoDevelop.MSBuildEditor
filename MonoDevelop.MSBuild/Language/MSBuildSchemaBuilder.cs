// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

using MonoDevelop.MSBuild.Analysis;
using MonoDevelop.MSBuild.Evaluation;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Parser;

namespace MonoDevelop.MSBuild.Language
{
	class MSBuildSchemaBuilder : MSBuildVisitor
	{
		MSBuildInferredSchema schema;
		readonly bool isToplevel;
		readonly MSBuildParserContext parseContext;
		readonly MSBuildImportResolver importResolver;

		public MSBuildSchemaBuilder (
			bool isToplevel, MSBuildParserContext parseContext,
			MSBuildImportResolver resolveImport)
		{
			this.isToplevel = isToplevel;
			this.parseContext = parseContext;
			this.importResolver = resolveImport;
		}

		protected override void BeforeRun ()
		{
			schema = Document.InferredSchema;
		}

		protected override void VisitResolvedElement (XElement element, MSBuildElementSyntax resolved)
		{
			try {
				CollectResolvedElement (element, resolved);
				base.VisitResolvedElement (element, resolved);
			} catch (Exception ex) when (isToplevel && IsNotCancellation (ex)) {
				Document.Diagnostics?.Add (CoreDiagnostics.InternalError, element.NameSpan, ex.Message);
				LoggingService.LogError ("Internal error in MSBuildDocumentValidator", ex);
			}
		}

		void CollectResolvedElement (XElement element, MSBuildElementSyntax resolved)
		{
			switch (resolved.SyntaxKind) {
			case MSBuildSyntaxKind.Import:
				ResolveImport (element);
				break;
			case MSBuildSyntaxKind.Item:
				CollectItem (element.Name.Name, ReferenceUsage.Write);
				break;
			case MSBuildSyntaxKind.Property:
				CollectProperty (element.Name.Name, ReferenceUsage.Write);
				break;
			case MSBuildSyntaxKind.UsingTask:
				CollectTaskDefinition (element);
				break;
			case MSBuildSyntaxKind.Task:
				CollectTask (element.Name.Name);
				break;
			case MSBuildSyntaxKind.Target:
				var targetName = element.Attributes.Get ("name", true)?.Value;
				if (!string.IsNullOrEmpty (targetName)) {
					CollectTarget (targetName);
				}
				break;
			case MSBuildSyntaxKind.Parameter:
				var taskName = element.ParentElement.ParentElement.Attributes.Get ("TaskName", true)?.Value;
				if (!string.IsNullOrEmpty (taskName)) {
					CollectTaskParameterDefinition (taskName, element);
				}
				break;
			case MSBuildSyntaxKind.Metadata:
				CollectMetadata (element.ParentElement.Name.Name, element.Name.Name, ReferenceUsage.Write);
				break;
			}
		}

		protected override void VisitResolvedAttribute (XElement element, XAttribute attribute, MSBuildElementSyntax resolvedElement, MSBuildAttributeSyntax resolvedAttribute)
		{
			if (resolvedAttribute.IsAbstract) {
				switch (resolvedElement.SyntaxKind) {
				case MSBuildSyntaxKind.Item:
					CollectMetadata (element.Name.Name, attribute.Name.Name, ReferenceUsage.Write);
					break;
				case MSBuildSyntaxKind.Task:
					CollectTaskParameter (element.Name.Name, attribute.Name.Name, false);
					break;
				}
			}
			if (resolvedElement.SyntaxKind == MSBuildSyntaxKind.Output && resolvedAttribute.Name == "TaskParameter") {
				CollectTaskParameter (element.ParentElement.Name.Name, attribute.Value, true);
			}
			base.VisitResolvedAttribute (element, attribute, resolvedElement, resolvedAttribute);
		}

		void ResolveImport (XElement element)
		{
			var importAtt = element.Attributes["Project"];
			var sdkAtt = element.Attributes["Sdk"];

			string import = null;

			if (!string.IsNullOrWhiteSpace (importAtt?.Value)) {
				import = importAtt.Value;
			}

			if (!string.IsNullOrWhiteSpace (sdkAtt?.Value)) {
				var loc = sdkAtt.ValueSpan;
				string sdkPath = parseContext.GetSdkPath (Document, sdkAtt.Value, loc);
				import = import == null ? null : sdkPath + "\\" + import;

				if (isToplevel && sdkPath != null) {
					Document.Annotations.Add (sdkAtt, new NavigationAnnotation (sdkPath, loc));
				}
			}

			if (import != null) {
				bool wasResolved = false;
				var loc = importAtt.ValueSpan;
				foreach (var resolvedImport in importResolver.Resolve (import, null)) {
					Document.AddImport (resolvedImport);
					wasResolved |= resolvedImport.IsResolved;
					if (isToplevel && wasResolved) {
						Document.Annotations.Add (importAtt, new NavigationAnnotation (resolvedImport.Filename, loc));
					}
				}
				if (!wasResolved && isToplevel) {
					DiagnosticSeverity type = element.Attributes.Get ("Condition", true) == null ? DiagnosticSeverity.Error : DiagnosticSeverity.Warning;
					Document.Diagnostics.Add (CoreDiagnostics.UnresolvedImport, loc, import);
				}
			}
		}

		protected override void VisitElementValue (XElement element, MSBuildElementSyntax resolved, string value, int offset)
		{
			var kind = resolved.ValueKind;

			if (resolved.SyntaxKind == MSBuildSyntaxKind.Property) {
				var name = element.Name.Name;
				parseContext.PropertyCollector.Collect (name, value);

				switch (element.Name.Name.ToLowerInvariant ()) {
				case "configuration":
					kind = MSBuildValueKind.Configuration;
					break;
				case "configurations":
					kind = MSBuildValueKind.Configuration.List ();
					break;
				case "platform":
					kind = MSBuildValueKind.Platform;
					break;
				case "platforms":
					kind = MSBuildValueKind.Platform.List ();
					break;
				}
			} else if (resolved.SyntaxKind == MSBuildSyntaxKind.Metadata && element.ParentElement.NameEquals ("ProjectConfiguration", true)) {
				if (element.NameEquals ("Configuration", true)) {
					kind = MSBuildValueKind.Configuration;
				} else if (element.NameEquals ("Platform", true)) {
					kind = MSBuildValueKind.Platform;
				}
			}

			ExtractReferences (kind, value, offset);
		}

		protected override void VisitAttributeValue (XElement element, XAttribute attribute, MSBuildElementSyntax resolvedElement, MSBuildAttributeSyntax resolvedAttribute, string value, int offset)
		{
			var kind = resolvedAttribute.ValueKind;

			if (resolvedElement.SyntaxKind == MSBuildSyntaxKind.Item && element.NameEquals ("ProjectConfiguration", true)) {
				if (attribute.NameEquals ("Configuration", true)) {
					kind = MSBuildValueKind.Configuration;
				} else if (attribute.NameEquals ("Platform", true)) {
					kind = MSBuildValueKind.Platform;
				}
			}

			ExtractReferences (kind, value, offset);
		}

		void CollectItem (string itemName, ReferenceUsage usage)
		{
			if (schema.ItemUsage.TryGetValue (itemName, out var existingUsage)) {
				if (existingUsage == usage) {
					return;
				}
				usage |= existingUsage;
			} else {
				schema.Items.Add (itemName, new ItemInfo (itemName, null));
			}
			schema.ItemUsage[itemName] = usage;
		}

		void CollectProperty (string propertyName, ReferenceUsage usage)
		{
			if (schema.PropertyUsage.TryGetValue (propertyName, out var existingUsage)) {
				if (existingUsage == usage) {
					return;
				}
				usage |= existingUsage;
			} else if (!Builtins.Properties.ContainsKey (propertyName)) {
				schema.Properties.Add (propertyName, new PropertyInfo (propertyName, null));
			}
			schema.PropertyUsage[propertyName] = usage;
		}

		void CollectTarget (string name)
		{
			if (name != null && !schema.Targets.ContainsKey (name)) {
				schema.Targets[name] = new TargetInfo (name, null);
			}
		}

		void CollectMetadata (string itemName, string metadataName, ReferenceUsage usage)
		{
			if (itemName == null) {
				return;
			}
			CollectItem (itemName, usage);
			if (schema.MetadataUsage.TryGetValue ((itemName, metadataName), out var existingUsage)) {
				if (existingUsage == usage) {
					return;
				}
				usage |= existingUsage;
			} else if (!Builtins.Metadata.ContainsKey (metadataName)) {
				var item = schema.Items[itemName];
				item.Metadata.Add (metadataName, new MetadataInfo (metadataName, null, item: item));
			}
			schema.MetadataUsage[(itemName, metadataName)] = usage;
		}

		void CollectTask (string name)
		{
			if (!schema.Tasks.TryGetValue (name, out TaskInfo task)) {
				schema.Tasks[name] = task = new TaskInfo (name, null, null, null, null, null, 0);
			}
		}

		void CollectTaskParameter (string taskName, string parameterName, bool isOutput)
		{
			var task = schema.Tasks[taskName];
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

		void CollectTaskParameterDefinition (string taskName, XElement def)
		{
			var task = schema.Tasks[taskName];
			var parameterName = def.Name.Name;
			if (task.Parameters.ContainsKey (parameterName)) {
				return;
			}

			bool isRequired = def.Attributes.IsTrue ("Required");
			bool isOutout = def.Attributes.IsTrue ("Output");

			var kind = MSBuildValueKind.Unknown;
			bool isList = false;

			var type = def.Attributes.Get ("ParameterType", true)?.Value;
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
				kind = kind.List ();
			}

			task.Parameters.Add (parameterName, new TaskParameterInfo (parameterName, null, isRequired, isOutout, kind));
		}

		void CollectTaskDefinition (XElement element)
		{
			string taskName = null, assemblyFile = null, assemblyName = null, taskFactory = null;
			foreach (var att in element.Attributes) {
				switch (att.Name.Name.ToLowerInvariant ()) {
				case "assemblyfile":
					assemblyFile = att.Value;
					break;
				case "assemblyname":
					assemblyName = att.Value;
					break;
				case "taskfactory":
					taskFactory = att.Value;
					break;
				case "taskname":
					taskName = att.Value;
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
				TaskInfo info = parseContext.TaskBuilder.CreateTaskInfo (taskName, assemblyName, assemblyFile, Filename, element.Span.Start, evalCtx);
				if (info != null) {
					schema.Tasks[info.Name] = info;
					return;
				}
			}

			//HACK: RoslynCodeTaskFactory determines the parameters automatically from the code, until we
			//can do this too we need to force inference
			bool forceInferAttributes = taskFactory != null && (
				string.Equals (taskFactory, "RoslynCodeTaskFactory", StringComparison.OrdinalIgnoreCase) || (
					string.Equals (taskFactory, "CodeTaskFactory", StringComparison.OrdinalIgnoreCase) &&
					string.Equals (assemblyFile, "$(RoslynCodeTaskFactory)", StringComparison.OrdinalIgnoreCase
				)) &&
				!element.Elements.Any (n => n.Name.Name == "ParameterGroup"));

			schema.Tasks[name] = new TaskInfo (name, null, null, null, null, Filename, element.Span.Start) {
				ForceInferAttributes = forceInferAttributes
			};
		}

		void ExtractReferences (MSBuildValueKind kind, string value, int startOffset)
		{
			ExpressionNode expression;

			//try-catch shouldn't be necessary, but it makes this more robust against bugs in the expression parser
			try {
				if (kind == MSBuildValueKind.Condition) {
					expression = ExpressionParser.ParseCondition (value, startOffset);
				} else {
					expression = ExpressionParser.Parse (value, ExpressionOptions.ItemsMetadataAndLists, startOffset);
				}
			} catch (Exception ex) {
				LoggingService.LogError ($"Error parsing MSBuild expression '{value}' in file {Filename ?? "[unnamed]"} at {startOffset}", ex);
				return;
			}

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
						value = literal.GetUnescapedValue ().Trim ();
						switch (kind.GetScalarType ()) {
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
							Document.InferredSchema.Configurations.Add (value);
							break;
						case MSBuildValueKind.Platform:
							Document.InferredSchema.Platforms.Add (value);
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
				Document.InferredSchema.Configurations.Add (value);
			} else if (string.Equals (prop.Name, "Platform", StringComparison.OrdinalIgnoreCase)) {
				Document.InferredSchema.Platforms.Add (value);
			}
		}
	}
}