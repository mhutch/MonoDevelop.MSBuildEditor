// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.Ide.Editor;
using MonoDevelop.Ide.TypeSystem;
using MonoDevelop.MSBuildEditor.Schema;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuildEditor.Language
{
	class MSBuildDocumentResolver : MSBuildVisitor
	{
		protected MSBuildResolveContext Context { get; }
		protected string Filename { get; }
		protected ITextDocument Document { get; }

		readonly bool isToplevel;
		readonly MSBuildSdkResolver sdkResolver;
		readonly PropertyValueCollector propertyValues;
		readonly ImportResolver resolveImport;

		public MSBuildDocumentResolver (
			MSBuildResolveContext ctx, string filename, bool isToplevel,
			ITextDocument textDocument, MSBuildSdkResolver sdkResolver,
			PropertyValueCollector propertyValues, ImportResolver resolveImport)
		{
			this.Context = ctx;
			this.Filename = filename;
			this.isToplevel = isToplevel;
			this.Document = textDocument;
			this.sdkResolver = sdkResolver;
			this.propertyValues = propertyValues;
			this.resolveImport = resolveImport;
		}

		protected override void VisitImport (XElement element, MSBuildLanguageElement resolved)
		{
			base.VisitImport (element, resolved);

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

		protected override void VisitItem (XElement element, MSBuildLanguageElement resolved)
		{
			var name = element.Name.Name;
			if (!Context.Items.TryGetValue (name, out ItemInfo item))
				Context.Items [name] = item = new ItemInfo (name, null);
			base.VisitItem (element, resolved);
		}

		protected override void VisitMetadata (XElement element, MSBuildLanguageElement resolved, string itemName, string metadataName)
		{
			base.VisitMetadata (element, resolved, itemName, metadataName);
		}

		protected override void VisitMetadataAttribute (XAttribute attribute, string itemName, string metadataName)
		{
			var item = Context.Items [itemName];
			if (!item.Metadata.ContainsKey (metadataName) && !Builtins.Metadata.ContainsKey (metadataName)) {
				item.Metadata.Add (metadataName, new MetadataInfo (metadataName, null, item: item));
			}
			base.VisitMetadataAttribute (attribute, itemName, metadataName);
		}

		protected override void VisitItemReference (string itemName, int start, int length)
		{
			var name = itemName;
			if (!Context.Items.ContainsKey (name)) {
				Context.Items.Add (name, new ItemInfo (name, null));
			}
			base.VisitItemReference (itemName, start, length);
		}

		protected override void VisitMetadataReference (string itemName, string metadataName, int start, int length)
		{
			if (itemName != null && Context.Items.TryGetValue (itemName, out ItemInfo item)) {
				if (!item.Metadata.ContainsKey (metadataName) && !Builtins.Metadata.ContainsKey (metadataName)) {
					item.Metadata.Add (metadataName, new MetadataInfo (metadataName, null, item: item));
				}
			}

			base.VisitMetadataReference (itemName, metadataName, start, length);
		}

		protected override void VisitProperty (XElement element, MSBuildLanguageElement resolved)
		{
			var name = element.Name.Name;
			propertyValues.Collect (name, element, Document);

			base.VisitProperty (element, resolved);
		}

		protected override void VisitPropertyReference (string propertyName, int start, int length)
		{
			if (!Context.Properties.ContainsKey (propertyName) && !Builtins.Properties.ContainsKey (propertyName)) {
				Context.Properties.Add (propertyName, new PropertyInfo (propertyName, null));
			}

			base.VisitPropertyReference (propertyName, start, length);
		}

		protected override void VisitTarget (XElement element, MSBuildLanguageElement resolved)
		{
			var name = element.Attributes.Get (new XName ("name"), true)?.Value;
			if (name != null && !Context.Targets.TryGetValue (name, out TargetInfo target)) {
				Context.Targets [name] = target = new TargetInfo (name, null);
			}
			base.VisitTarget (element, resolved);
		}

		protected override void VisitTask (XElement element, MSBuildLanguageElement resolved)
		{
			var name = element.Name.Name;
			if (!Context.Tasks.TryGetValue (name, out TaskInfo task)) {
				Context.Tasks [name] = task = new TaskInfo (name, null);
			}
			base.VisitTask (element, resolved);
		}

		protected override void VisitTaskParameter (XAttribute attribute, string taskName, string parameterName)
		{
			var task = Context.Tasks [taskName];
			if (!task.Parameters.ContainsKey (parameterName)) {
				task.Parameters.Add (parameterName, new TaskParameterInfo (parameterName, null));
			}

			base.VisitTaskParameter (attribute, taskName, parameterName);
		}
    }
}