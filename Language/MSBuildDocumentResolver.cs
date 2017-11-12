// Copyright (c) Microsoft. ALl rights reserved.
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

		public MSBuildDocumentResolver (MSBuildResolveContext ctx, string filename, bool isToplevel, XDocument doc, ITextDocument textDocument, MSBuildSdkResolver sdkResolver, PropertyValueCollector propVals, ImportResolver resolveImport)
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

		protected override void VisitImport (XElement element)
		{
			base.VisitImport (element);

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
				foreach (var resolvedImport in resolveImport (ctx, import, propertyValues)) {
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

		protected override void VisitItem (XElement element)
		{
			var name = element.Name.Name;
			if (!ctx.Items.TryGetValue (name, out ItemInfo item))
				ctx.Items [name] = item = new ItemInfo (name, null);
			base.VisitItem (element);
		}

		protected override void VisitMetadata (XElement element, string itemName, string metadataName)
		{
			var item = ctx.Items [itemName];
			if (!item.Metadata.ContainsKey (metadataName) && !Builtins.Metadata.ContainsKey (metadataName))
				item.Metadata.Add (metadataName, new MetadataInfo (metadataName, null, false));
			base.VisitMetadata (element, itemName, metadataName);
		}

		protected override void VisitMetadataAttribute (XAttribute attribute, string itemName, string metadataName)
		{
			var item = ctx.Items [itemName];
			if (!item.Metadata.ContainsKey (metadataName) && !Builtins.Metadata.ContainsKey (metadataName)) {
				item.Metadata.Add (metadataName, new MetadataInfo (metadataName, null, false));
			}
			base.VisitMetadataAttribute (attribute, itemName, metadataName);
		}

		protected override void VisitItemReference (string itemName, int start, int length)
		{
			base.VisitItemReference (itemName, start, length);
		}

		protected override void VisitMetadataReference (string itemName, string metadataName, int start, int length)
		{
			base.VisitMetadataReference (itemName, metadataName, start, length);
		}

		protected override void VisitProperty (XElement element)
		{
			var name = element.Name.Name;
			if (!ctx.Properties.ContainsKey (name) && !Builtins.Properties.ContainsKey (name)) {
				ctx.Properties.Add (name, new PropertyInfo (name, null, false, false));
			}
			propertyValues.Collect (name, element, textDocument);

			base.VisitProperty (element);
		}

		protected override void VisitPropertyReference (string propertyName, int start, int length)
		{
			base.VisitPropertyReference (propertyName, start, length);
		}

		protected override void VisitResolved (XElement element, MSBuildSchemaElement resolved)
		{
			if (isToplevel) {
				ValidateAttributes (element, resolved);
			}

			base.VisitResolved (element, resolved);
		}

		protected override void VisitTarget (XElement element)
		{
			base.VisitTarget (element);
		}

		protected override void VisitTask (XElement element)
		{
			var name = element.Name.Name;
			TaskInfo task;
			if (!ctx.Tasks.TryGetValue (name, out task)) {
				ctx.Tasks [name] = task = new TaskInfo (name, null);
			}
			base.VisitTask (element);
		}

		protected override void VisitTaskParameter (XAttribute attribute, string taskName, string parameterName)
		{
			var task = ctx.Tasks [taskName];
			task.Parameters.Add (parameterName);

			base.VisitTaskParameter (attribute, taskName, parameterName);
		}

		protected override void VisitUnknown (XElement element)
		{
			base.VisitUnknown (element);
		}

		void ValidateAttributes (XElement element, MSBuildSchemaElement kind)
		{
			//TODO these need special handling
			if (kind.Kind == MSBuildKind.Item || kind.Kind == MSBuildKind.Task) {
				return;
			}

			//TODO: check required attributes
			//TODO: validate attribute expressions
			foreach (var att in element.Attributes) {
				var valid = kind.Attributes.Any (a => att.Name.FullName == a);
				if (!valid) {
					ctx.Errors.Add (new Error (ErrorType.Error, $"Unknown attribute '{att.Name.FullName}'", att.Region));
				}
			}
		}
	}
}