// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using MonoDevelop.Core;
using MonoDevelop.Ide.TypeSystem;
using MonoDevelop.MSBuildEditor.Schema;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuildEditor.Language
{
	class MSBuildSchemaBuilder : MSBuildVisitor
	{
		readonly bool isToplevel;
		readonly IRuntimeInformation runtime;
		readonly PropertyValueCollector propertyValues;
		readonly ImportResolver resolveImport;

		public MSBuildSchemaBuilder (
			bool isToplevel, IRuntimeInformation runtime,
			PropertyValueCollector propertyValues, ImportResolver resolveImport)
		{
			this.isToplevel = isToplevel;
			this.runtime = runtime;
			this.propertyValues = propertyValues;
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
				CollectTaskParameter (element.ParentElement ().Name.Name, element.Name.Name);
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
					CollectTaskParameter (element.Name.Name, attribute.Name.Name);
					break;
				}
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
				foreach (var resolvedImport in resolveImport (import, null, propertyValues)) {
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
			if (resolved.Kind == MSBuildKind.Property) {
				var name = element.Name.Name;
				propertyValues.Collect (name, value);
			}
			ExtractReferences (resolved.ValueKind, value, offset);
		}

		protected override void VisitAttributeValue (XElement element, XAttribute attribute, MSBuildLanguageAttribute resolvedAttribute, string value, int offset)
		{
			ExtractReferences (resolvedAttribute.ValueKind, value, offset);
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
				Document.Tasks [name] = task = new TaskInfo (name, null);
			}
		}

		void CollectTaskParameter (string taskName, string parameterName)
		{
			var task = Document.Tasks [taskName];
			if (!task.Parameters.ContainsKey (parameterName)) {
				task.Parameters.Add (parameterName, new TaskParameterInfo (parameterName, null));
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
							switch (kind) {
							case MSBuildValueKind.ItemName:
								CollectItem (literal.Value);
								break;
							case MSBuildValueKind.TargetName:
								CollectTarget (literal.Value);
								break;
							case MSBuildValueKind.PropertyName:
								CollectProperty (literal.Value);
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