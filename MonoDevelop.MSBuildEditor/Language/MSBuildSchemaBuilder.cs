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
		readonly MSBuildSdkResolver sdkResolver;
		readonly PropertyValueCollector propertyValues;
		readonly ImportResolver resolveImport;

		public MSBuildSchemaBuilder (
			bool isToplevel, MSBuildSdkResolver sdkResolver,
			PropertyValueCollector propertyValues, ImportResolver resolveImport)
		{
			this.isToplevel = isToplevel;
			this.sdkResolver = sdkResolver;
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
				var loc = isToplevel? sdkAtt.GetValueRegion (Document) : sdkAtt.Region;
				sdkPath = Context.GetSdkPath (sdkResolver, sdkAtt.Value, loc);
				import = import == null? null : sdkPath + "\\" + import;

				if (isToplevel) {
					Context.Annotations.Add (sdkAtt, new NavigationAnnotation (sdkPath, loc));
				}
			}

			if (import != null) {
				bool wasResolved = false;
				var loc = isToplevel ? importAtt.GetValueRegion (Document) : importAtt.Region;
				foreach (var resolvedImport in resolveImport (Context, import, null, propertyValues)) {
					Context.Imports [resolvedImport.Filename] = resolvedImport;
					wasResolved |= resolvedImport.IsResolved;
					if (isToplevel) {
						Context.Annotations.Add (importAtt, new NavigationAnnotation (resolvedImport.Filename, loc));
					}
				}
				if (!wasResolved && isToplevel) {
					Context.Errors.Add (new Error (ErrorType.Error, "Could not resolve import", loc));
				}
			}
		}

		protected override void VisitElementValue (XElement element, MSBuildLanguageElement resolved, string value, int offset)
		{
			if (resolved.Kind == MSBuildKind.Property) {
				var name = element.Name.Name;
				propertyValues.Collect (name, value);
			}
			ExtractReferences (value, offset);
			base.VisitElementValue (element, resolved, value, offset);
		}

		void CollectItem (string itemName)
		{
			var name = itemName;
			if (!Context.Items.ContainsKey (name)) {
				Context.Items.Add (name, new ItemInfo (name, null));
			}
		}

		void CollectProperty (string propertyName)
		{
			if (!Context.Properties.ContainsKey (propertyName) && !Builtins.Properties.ContainsKey (propertyName)) {
				Context.Properties.Add (propertyName, new PropertyInfo (propertyName, null));
			}
		}

		void CollectTarget (string name)
		{
			if (name != null && !Context.Targets.TryGetValue (name, out TargetInfo target)) {
				Context.Targets [name] = target = new TargetInfo (name, null);
			}
		}

		void CollectMetadata (string itemName, string metadataName)
		{
			if (itemName != null && Context.Items.TryGetValue (itemName, out ItemInfo item)) {
				if (!item.Metadata.ContainsKey (metadataName) && !Builtins.Metadata.ContainsKey (metadataName)) {
					item.Metadata.Add (metadataName, new MetadataInfo (metadataName, null, item: item));
				}
			}
		}

		void CollectTask (string name)
		{
			if (!Context.Tasks.TryGetValue (name, out TaskInfo task)) {
				Context.Tasks [name] = task = new TaskInfo (name, null);
			}
		}

		void CollectTaskParameter (string taskName, string parameterName)
		{
			var task = Context.Tasks [taskName];
			if (!task.Parameters.ContainsKey (parameterName)) {
				task.Parameters.Add (parameterName, new TaskParameterInfo (parameterName, null));
			}
		}

		void ExtractReferences (string value, int startOffset)
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
					}
				}
			} catch (Exception ex) {
				LoggingService.LogError ($"Error parsing MSBuild expression at {startOffset}", ex);
			}
		}
    }
}