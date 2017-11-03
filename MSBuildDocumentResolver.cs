//
// Copyright (c) 2017 Microsoft Corp.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System.Linq;
using MonoDevelop.Ide.Editor;
using MonoDevelop.Ide.TypeSystem;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuildEditor
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
			var importAtt = element.Attributes [new XName ("Project")];
			var sdkAtt = element.Attributes [new XName ("Sdk")];
			if (importAtt != null) {
				foreach (var import in resolveImport (ctx, importAtt.Value, sdkAtt?.Value, importAtt.Region, sdkAtt?.Region ?? DocumentRegion.Empty, propertyValues)) {
					ctx.Imports [import.Filename] = import;
					if (isToplevel) {
						ctx.Annotations.Add (importAtt, import);
					}
				}
			}
			base.VisitImport (element);
		}

		protected override void VisitItem (XElement element)
		{
			var name = element.Name.Name;
			ItemInfo item;
			if (!ctx.Items.TryGetValue (name, out item))
				ctx.Items [name] = item = new ItemInfo (name, null);
			base.VisitItem (element);
		}

		protected override void VisitMetadata (XElement element, string itemName, string metadataName)
		{
			var item = ctx.Items [itemName];
			if (!item.Metadata.ContainsKey (metadataName) && !Builtins.Metadata.ContainsKey (metadataName))
				item.Metadata.Add (metadataName, new MetadataInfo (metadataName, null));
			base.VisitMetadata (element, itemName, metadataName);
		}

		protected override void VisitMetadataAttribute (XAttribute attribute, string itemName, string metadataName)
		{
			var item = ctx.Items [itemName];
			if (!item.Metadata.ContainsKey (metadataName) && !Builtins.Metadata.ContainsKey (metadataName)) {
				item.Metadata.Add (metadataName, new MetadataInfo (metadataName, null));
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
				ctx.Properties.Add (name, new PropertyInfo (name, null));
			}
			propertyValues.Collect (name, element, textDocument);

			base.VisitProperty (element);
		}

		protected override void VisitPropertyReference (string propertyName, int start, int length)
		{
			base.VisitPropertyReference (propertyName, start, length);
		}

		protected override void VisitResolved (XElement element, MSBuildElement resolved)
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

		void ValidateAttributes (XElement element, MSBuildElement kind)
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