// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Projects.MSBuild.Conditions;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Parser;

namespace MonoDevelop.MSBuild.Language
{
	class MSBuildSchemaBuilder : MSBuildVisitor
	{
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

		protected override void VisitResolvedElement (XElement element, MSBuildLanguageElement resolved)
		{
			try {
				CollectResolvedElement (element, resolved);
				base.VisitResolvedElement (element, resolved);
			} catch (Exception ex) when (isToplevel) {
				Document.Errors.Add (new XmlDiagnosticInfo (DiagnosticSeverity.Error, $"Internal error: {ex.Message}", element.GetNameSpan ()));
				LoggingService.LogError ("Internal error in MSBuildDocumentValidator", ex);
			}
		}

		void CollectResolvedElement (XElement element, MSBuildLanguageElement resolved)
		{
			switch (resolved.Kind) {
			case MSBuildKind.Import:
				ResolveImport (element);
				break;
			case MSBuildKind.Item:
				CollectItem (element.Name.Name);
				break;
			case MSBuildKind.Property:
				CollectProperty (element.Name.Name);
				break;
			case MSBuildKind.UsingTask:
				CollectTaskDefinition (element);
				break;
			case MSBuildKind.Task:
				CollectTask (element.Name.Name);
				break;
			case MSBuildKind.Target:
				var targetName = element.Attributes.Get (new XName ("name"), true)?.Value;
				if (targetName != null) {
					CollectTarget (targetName);
				}
				break;
			case MSBuildKind.Parameter:
				var taskName = element.ParentElement ().ParentElement ().Attributes.Get (new XName ("TaskName"), true)?.Value;
				if (!string.IsNullOrEmpty (taskName)) {
					CollectTaskParameterDefinition (taskName, element);
				}
				break;
			case MSBuildKind.Metadata:
				CollectMetadata (element.ParentElement ().Name.Name, element.Name.Name);
				break;
			}
		}

		protected override void VisitResolvedAttribute (XElement element, XAttribute attribute, MSBuildLanguageElement resolvedElement, MSBuildLanguageAttribute resolvedAttribute)
		{
			if (resolvedAttribute.IsAbstract) {
				switch (resolvedElement.Kind) {
				case MSBuildKind.Item:
					CollectMetadata (element.Name.Name, attribute.Name.Name);
					break;
				case MSBuildKind.Task:
					CollectTaskParameter (element.Name.Name, attribute.Name.Name, false);
					break;
				}
			}
			if (resolvedElement.Kind == MSBuildKind.Output && resolvedAttribute.Name == "TaskParameter") {
				CollectTaskParameter (element.ParentElement ().Name.Name, attribute.Value, true);
			}
			base.VisitResolvedAttribute (element, attribute, resolvedElement, resolvedAttribute);
		}

		void ResolveImport (XElement element)
		{
			var importAtt = element.Attributes[new XName ("Project")];
			var sdkAtt = element.Attributes[new XName ("Sdk")];

			string sdkPath = null, import = null;

			if (!string.IsNullOrWhiteSpace (importAtt?.Value)) {
				import = importAtt.Value;
			}

			if (!string.IsNullOrWhiteSpace (sdkAtt?.Value)) {
				var loc = sdkAtt.GetValueSpan ();
				sdkPath = parseContext.GetSdkPath (Document, sdkAtt.Value, loc);
				import = import == null ? null : sdkPath + "\\" + import;

				if (isToplevel && sdkPath != null) {
					Document.Annotations.Add (sdkAtt, new NavigationAnnotation (sdkPath, loc));
				}
			}

			if (import != null) {
				bool wasResolved = false;
				var loc = importAtt.GetValueSpan ();
				foreach (var resolvedImport in importResolver.Resolve (import, null)) {
					Document.AddImport (resolvedImport);
					wasResolved |= resolvedImport.IsResolved;
					if (isToplevel && wasResolved) {
						Document.Annotations.Add (importAtt, new NavigationAnnotation (resolvedImport.Filename, loc));
					}
				}
				if (!wasResolved && isToplevel) {
					DiagnosticSeverity type = element.Attributes.Get (new XName ("Condition"), true) == null ? DiagnosticSeverity.Error : DiagnosticSeverity.Warning;
					Document.Errors.Add (new XmlDiagnosticInfo (type, "Could not resolve import", loc));
				}
			}
		}

		protected override void VisitElementValue (XElement element, MSBuildLanguageElement resolved, string value, int offset)
		{
			var kind = resolved.ValueKind;

			if (resolved.Kind == MSBuildKind.Property) {
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
			} else if (resolved.Kind == MSBuildKind.Metadata && element.ParentElement ().NameEquals ("ProjectConfiguration", true)) {
				if (element.NameEquals ("Configuration", true)) {
					kind = MSBuildValueKind.Configuration;
				} else if (element.NameEquals ("Platform", true)) {
					kind = MSBuildValueKind.Platform;
				}
			}

			ExtractReferences (kind, value, offset);
		}

		protected override void VisitAttributeValue (XElement element, XAttribute attribute, MSBuildLanguageElement resolvedElement, MSBuildLanguageAttribute resolvedAttribute, string value, int offset)
		{
			var kind = resolvedAttribute.ValueKind;

			//FIXME ExtractConfigurations should directly handle extracting references
			if (kind == MSBuildValueKind.Condition) {
				ExtractConfigurations (value, offset);
			}

			if (resolvedElement.Kind == MSBuildKind.Item && element.NameEquals ("ProjectConfiguration", true)) {
				if (attribute.NameEquals ("Configuration", true)) {
					kind = MSBuildValueKind.Configuration;
				} else if (attribute.NameEquals ("Platform", true)) {
					kind = MSBuildValueKind.Platform;
				}
			}

			ExtractReferences (kind, value, offset);
		}

		void CollectItem (string itemName)
		{
			var name = itemName;
			if (!Document.Items.ContainsKey (name)) {
				Document.Items.Add (name, new ItemInfo (name, null));
			}
		}

		void CollectProperty (string propertyName)
		{
			if (!Document.Properties.ContainsKey (propertyName) && !Builtins.Properties.ContainsKey (propertyName)) {
				Document.Properties.Add (propertyName, new PropertyInfo (propertyName, null));
			}
		}

		void CollectTarget (string name)
		{
			if (name != null && !Document.Targets.TryGetValue (name, out TargetInfo target)) {
				Document.Targets[name] = target = new TargetInfo (name, null);
			}
		}

		void CollectMetadata (string itemName, string metadataName)
		{
			if (itemName != null && Document.Items.TryGetValue (itemName, out ItemInfo item)) {
				if (!item.Metadata.ContainsKey (metadataName) && !Builtins.Metadata.ContainsKey (metadataName)) {
					item.Metadata.Add (metadataName, new MetadataInfo (metadataName, null, item: item));
				}
			}
		}

		void CollectTask (string name)
		{
			if (!Document.Tasks.TryGetValue (name, out TaskInfo task)) {
				Document.Tasks[name] = task = new TaskInfo (name, null, null, null, null, null, 0);
			}
		}

		void CollectTaskParameter (string taskName, string parameterName, bool isOutput)
		{
			var task = Document.Tasks[taskName];
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
			var task = Document.Tasks[taskName];
			var parameterName = def.Name.Name;
			if (task.Parameters.ContainsKey (parameterName)) {
				return;
			}

			bool isRequired = def.Attributes.IsTrue ("Required");
			bool isOutout = def.Attributes.IsTrue ("Output");

			var kind = MSBuildValueKind.Unknown;
			bool isList = false;

			var type = def.Attributes.Get (new XName ("ParameterType"), true)?.Value;
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
				TaskInfo info = parseContext.TaskBuilder.CreateTaskInfo (taskName, assemblyName, assemblyFile, Filename, element.Span.Start, parseContext.PropertyCollector);
				if (info != null) {
					Document.Tasks[info.Name] = info;
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

			Document.Tasks[taskName] = new TaskInfo (taskName, null, null, null, null, Filename, element.Span.Start) {
				ForceInferAttributes = forceInferAttributes
			};
		}

		void ExtractConfigurations (string value, int startOffset)
		{
			try {
				value = XmlEscaping.UnescapeEntities (value);
				var cond = ConditionParser.ParseCondition (value);
				cond.CollectConditionProperties (Document);
			} catch (Exception ex) {
				LoggingService.LogError ($"Error parsing MSBuild condition at {Filename ?? "[unnamed]"}:{startOffset}", ex);
			}
		}

		void ExtractReferences (MSBuildValueKind kind, string value, int startOffset)
		{
			try {
				var expression = ExpressionParser.Parse (value, ExpressionOptions.ItemsMetadataAndLists, startOffset);
				foreach (var node in expression.WithAllDescendants ()) {
					switch (node) {
					case ExpressionPropertyName prop:
						CollectProperty (prop.Name);
						break;
					case ExpressionItemName item:
						CollectItem (item.Name);
						break;
					case ExpressionMetadata meta:
						var itemName = meta.GetItemName ();
						if (itemName != null) {
							CollectMetadata (itemName, meta.MetadataName);
						}
						break;
					case ExpressionText literal:
						if (literal.IsPure) {
							value = literal.GetUnescapedValue ().Trim ();
							switch (kind.GetScalarType ()) {
							case MSBuildValueKind.ItemName:
								CollectItem (value);
								break;
							case MSBuildValueKind.TargetName:
								CollectTarget (value);
								break;
							case MSBuildValueKind.PropertyName:
								CollectProperty (value);
								break;
							case MSBuildValueKind.Configuration:
								Document.Configurations.Add (value);
								break;
							case MSBuildValueKind.Platform:
								Document.Platforms.Add (value);
								break;
							}
						}
						break;
					}
				}
			} catch (Exception ex) {
				LoggingService.LogError ($"Error parsing MSBuild expression '{value}' in file {Filename ?? "[unnamed]"} at {startOffset}", ex);
			}
		}
	}
}