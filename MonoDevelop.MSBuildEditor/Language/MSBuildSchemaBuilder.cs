// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using MonoDevelop.Core;
using MonoDevelop.Ide.TypeSystem;
using MonoDevelop.MSBuildEditor.Schema;
using MonoDevelop.Projects.MSBuild.Conditions;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuildEditor.Language
{
	class MSBuildSchemaBuilder : MSBuildVisitor
	{
		readonly bool isToplevel;
		readonly IRuntimeInformation runtime;
		readonly PropertyValueCollector propertyValues;
		readonly TaskMetadataBuilder taskMetadataBuilder;
		readonly ImportResolver resolveImport;

		public MSBuildSchemaBuilder (
			bool isToplevel, IRuntimeInformation runtime,
			PropertyValueCollector propertyValues,
			TaskMetadataBuilder taskBuilder,
			ImportResolver resolveImport)
		{
			this.isToplevel = isToplevel;
			this.runtime = runtime;
			this.propertyValues = propertyValues;
			this.taskMetadataBuilder = taskBuilder;
			this.resolveImport = resolveImport;
		}

		protected override void VisitResolvedElement (XElement element, MSBuildLanguageElement resolved)
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
				CollectTaskParameterDefinition (element.ParentElement ().Name.Name, element);
				break;
			case MSBuildKind.Metadata:
				CollectMetadata (element.ParentElement ().Name.Name, element.Name.Name);
				break;
			}
			base.VisitResolvedElement (element, resolved);
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
				CollectTaskParameter (element.Name.Name, attribute.Name.Name, true);
			}
			base.VisitResolvedAttribute (element, attribute, resolvedElement, resolvedAttribute);
		}

        void ResolveImport (XElement element)
		{
			var importAtt = element.Attributes [new XName ("Project")];
			var sdkAtt = element.Attributes [new XName ("Sdk")];

			string sdkPath = null, import = null;

			if (!string.IsNullOrWhiteSpace (importAtt?.Value)) {
				import = importAtt.Value;
			}

			if (!string.IsNullOrWhiteSpace (sdkAtt?.Value)) {
				var loc = isToplevel? sdkAtt.GetValueRegion (TextDocument) : sdkAtt.Region;
				sdkPath = Document.GetSdkPath (runtime, sdkAtt.Value, loc);
				import = import == null? null : sdkPath + "\\" + import;

				if (isToplevel) {
					Document.Annotations.Add (sdkAtt, new NavigationAnnotation (sdkPath, loc));
				}
			}

			if (import != null) {
				bool wasResolved = false;
				var loc = isToplevel ? importAtt.GetValueRegion (TextDocument) : importAtt.Region;
				foreach (var resolvedImport in resolveImport (import, null)) {
					Document.Imports [resolvedImport.Filename] = resolvedImport;
					wasResolved |= resolvedImport.IsResolved;
					if (isToplevel) {
						Document.Annotations.Add (importAtt, new NavigationAnnotation (resolvedImport.Filename, loc));
					}
				}
				if (!wasResolved && isToplevel) {
					ErrorType type = element.Attributes.Get (new XName ("Condition"), true) == null ? ErrorType.Error : ErrorType.Warning;
					Document.Errors.Add (new Error (type, "Could not resolve import", loc));
				}
			}
		}

		protected override void VisitElementValue (XElement element, MSBuildLanguageElement resolved, string value, int offset)
		{
			var kind = resolved.ValueKind;

			if (resolved.Kind == MSBuildKind.Property) {
				var name = element.Name.Name;
				propertyValues.Collect (name, value);

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
				Document.Targets [name] = target = new TargetInfo (name, null);
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
				Document.Tasks [name] = task = new TaskInfo (name, null, null, null, null, null, Ide.Editor.DocumentLocation.Empty);
			}
		}

		void CollectTaskParameter (string taskName, string parameterName, bool isOutput)
		{
			var task = Document.Tasks [taskName];
			if (task.IsInferred) {
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
			var task = Document.Tasks [taskName];
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
					type = type.Substring (type.Length - 2);
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
			string taskName = null, assemblyFile = null, assemblyName = null;
			foreach (var att in element.Attributes) {
				if (att.NameEquals ("TaskName", true)) {
					taskName = att.Value;
				} else if (att.NameEquals ("AssemblyFile", true)) {
					assemblyFile = att.Value;
				} else if (att.NameEquals ("AssemblyName", true)) {
					assemblyName = att.Value;
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

			var info = taskMetadataBuilder.CreateTaskInfo (taskName, assemblyName, assemblyFile, Filename, element.Region.Begin);
			if (info != null) {
				Document.Tasks [info.Name] = info;
			}
		}

		void ExtractConfigurations (string value, int startOffset)
		{
			try {
				var cond = ConditionParser.ParseCondition (value);
				cond.CollectConditionProperties (Document);
			} catch (Exception ex) {
				LoggingService.LogError ($"Error parsing MSBuild condition at {startOffset}", ex);
			}
		}

		void ExtractReferences (MSBuildValueKind kind, string value, int startOffset)
		{
			try {
				var expression = ExpressionParser.Parse (value, ExpressionOptions.ItemsMetadataAndLists, startOffset);
				foreach (var node in expression.WithAllDescendants ()) {
					switch (node) {
					case ExpressionProperty prop:
						CollectProperty (prop.Name);
						break;
					case ExpressionItem item:
						CollectItem (item.Name);
						break;
					case ExpressionMetadata meta:
						var itemName = meta.GetItemName ();
						if(itemName != null) {
							CollectMetadata (itemName, meta.MetadataName);
						}
						break;
					case ExpressionLiteral literal:
						if (literal.IsPure) {
							switch (kind.GetScalarType ()) {
							case MSBuildValueKind.ItemName:
								CollectItem (literal.Value);
								break;
							case MSBuildValueKind.TargetName:
								CollectTarget (literal.Value);
								break;
							case MSBuildValueKind.PropertyName:
								CollectProperty (literal.Value);
								break;
							case MSBuildValueKind.Configuration:
								Document.Configurations.Add (literal.Value);
								break;
							case MSBuildValueKind.Platform:
								Document.Platforms.Add (literal.Value);
								break;
							}
						}
						break;
					}
				}
			} catch (Exception ex) {
				LoggingService.LogError ($"Error parsing MSBuild expression at {startOffset}", ex);
			}
		}
    }
}