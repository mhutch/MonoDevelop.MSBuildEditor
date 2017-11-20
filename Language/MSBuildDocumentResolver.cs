// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using MonoDevelop.Ide.Editor;
using MonoDevelop.Ide.TypeSystem;
using MonoDevelop.MSBuildEditor.Schema;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuildEditor.Language
{
	class MSBuildDocumentResolver : MSBuildVisitor
	{
		readonly MSBuildResolveContext ctx;
		readonly string filename;
		readonly bool isToplevel;
		readonly XDocument doc;
		readonly ITextDocument textDocument;
		readonly MSBuildSdkResolver sdkResolver;
		readonly PropertyValueCollector propertyValues;
		readonly ImportResolver resolveImport;

		public MSBuildDocumentResolver (
			MSBuildResolveContext ctx, string filename, bool isToplevel, XDocument doc,
			ITextDocument textDocument, MSBuildSdkResolver sdkResolver,
			PropertyValueCollector propVals, ImportResolver resolveImport)
		{
			this.ctx = ctx;
			this.filename = filename;
			this.isToplevel = isToplevel;
			this.doc = doc;
			this.textDocument = textDocument;
			this.sdkResolver = sdkResolver;
			this.propertyValues = propVals;
			this.resolveImport = resolveImport;
		}

		protected override void VisitImport (XElement element, MSBuildLanguageElement resolved)
		{
			base.VisitImport (element, resolved);

			var importAtt = element.Attributes [new XName ("Project")];
			var sdkAtt = element.Attributes [new XName ("Sdk")];

			string sdkPath = null, import = null;

			if (importAtt != null && CheckValue (importAtt)) {
				import = importAtt.Value;
			}

			if (sdkAtt != null && CheckValue (sdkAtt)) {
				var loc = isToplevel? sdkAtt.GetValueRegion (textDocument) : sdkAtt.Region;
				sdkPath = ctx.GetSdkPath (sdkResolver, sdkAtt.Value, loc);
				import = import == null? null : sdkPath + "\\" + import;

				if (isToplevel) {
					ctx.Annotations.Add (sdkAtt, new NavigationAnnotation (sdkPath, loc));
				}
			}

			if (import != null) {
				bool wasResolved = false;
				var loc = isToplevel ? importAtt.GetValueRegion (textDocument) : importAtt.Region;
				foreach (var resolvedImport in resolveImport (ctx, import, null, propertyValues)) {
					ctx.Imports [resolvedImport.Filename] = resolvedImport;
					wasResolved |= resolvedImport.IsResolved;
					if (isToplevel) {
						ctx.Annotations.Add (importAtt, new NavigationAnnotation (resolvedImport.Filename, loc));
					}
				}
				if (!wasResolved && isToplevel) {
					ctx.Errors.Add (new Error (ErrorType.Error, "Could not resolve import", loc));
				}
			}
		}

		bool CheckValue (XAttribute att)
		{
			if (!string.IsNullOrWhiteSpace (att.Value)) {
				return true;
			}

			if (isToplevel) {
				ctx.Errors.Add (new Error (ErrorType.Error, "Empty value", att.Region));
			}

			return false;
		}

		protected override void VisitItem (XElement element, MSBuildLanguageElement resolved)
		{
			var name = element.Name.Name;
			if (!ctx.Items.TryGetValue (name, out ItemInfo item))
				ctx.Items [name] = item = new ItemInfo (name, null);
			base.VisitItem (element, resolved);
		}

		protected override void VisitMetadata (XElement element, MSBuildLanguageElement resolved, string itemName, string metadataName)
		{
			base.VisitMetadata (element, resolved, itemName, metadataName);
		}

		protected override void VisitMetadataAttribute (XAttribute attribute, string itemName, string metadataName)
		{
			var item = ctx.Items [itemName];
			if (!item.Metadata.ContainsKey (metadataName) && !Builtins.Metadata.ContainsKey (metadataName)) {
				item.Metadata.Add (metadataName, new MetadataInfo (metadataName, null, item: item));
			}
			base.VisitMetadataAttribute (attribute, itemName, metadataName);
		}

		protected override void VisitItemReference (string itemName, int start, int length)
		{
			var name = itemName;
			if (!ctx.Items.ContainsKey (name)) {
				ctx.Items.Add (name, new ItemInfo (name, null));
			}
			base.VisitItemReference (itemName, start, length);
		}

		protected override void VisitMetadataReference (string itemName, string metadataName, int start, int length)
		{
			if (itemName != null && ctx.Items.TryGetValue (itemName, out ItemInfo item)) {
				if (!item.Metadata.ContainsKey (metadataName) && !Builtins.Metadata.ContainsKey (metadataName)) {
					item.Metadata.Add (metadataName, new MetadataInfo (metadataName, null, item: item));
				}
			}

			base.VisitMetadataReference (itemName, metadataName, start, length);
		}

		protected override void VisitProperty (XElement element, MSBuildLanguageElement resolved)
		{
			var name = element.Name.Name;
			propertyValues.Collect (name, element, textDocument);

			base.VisitProperty (element, resolved);
		}

		protected override void VisitPropertyReference (string propertyName, int start, int length)
		{
			if (!ctx.Properties.ContainsKey (propertyName) && !Builtins.Properties.ContainsKey (propertyName)) {
				ctx.Properties.Add (propertyName, new PropertyInfo (propertyName, null));
			}

			base.VisitPropertyReference (propertyName, start, length);
		}

		protected override void VisitTarget (XElement element, MSBuildLanguageElement resolved)
		{
			var name = element.Attributes.Get (new XName ("name"), true)?.Value;
			if (name != null && !ctx.Targets.TryGetValue (name, out TargetInfo target)) {
				ctx.Targets [name] = target = new TargetInfo (name, null);
			}
			base.VisitTarget (element, resolved);
		}

		protected override void VisitTask (XElement element, MSBuildLanguageElement resolved)
		{
			var name = element.Name.Name;
			if (!ctx.Tasks.TryGetValue (name, out TaskInfo task)) {
				ctx.Tasks [name] = task = new TaskInfo (name, null);
			}
			base.VisitTask (element, resolved);
		}

		protected override void VisitTaskParameter (XAttribute attribute, string taskName, string parameterName)
		{
			var task = ctx.Tasks [taskName];
			if (!task.Parameters.ContainsKey (parameterName)) {
				task.Parameters.Add (parameterName, new TaskParameterInfo (parameterName, null));
			}

			base.VisitTaskParameter (attribute, taskName, parameterName);
		}

		protected override void VisitUnknownElement (XElement element)
		{
			if (isToplevel) {
				ctx.Errors.Add (new Error (ErrorType.Error, $"Unknown element '{element.Name.FullName}'", element.Region));
			}
			base.VisitUnknownElement (element);
		}

		protected override void VisitUnknownAttribute (XElement element, XAttribute attribute)
		{
			if (isToplevel) {
				ctx.Errors.Add (new Error (ErrorType.Error, $"Unknown attribute '{attribute.Name.FullName}'", attribute.Region));
			}
			base.VisitUnknownAttribute (element, attribute);
		}
	}
}