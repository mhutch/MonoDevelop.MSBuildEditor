//
// MSBuildResolveContext.cs
//
// Author:
//       Mikayla Hutchinson <m.j.hutchinson@gmail.com>
//
// Copyright (c) 2014 Xamarin Inc.
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MonoDevelop.Core;
using MonoDevelop.Ide.Editor;
using MonoDevelop.MSBuildEditor.ExpressionParser;
using MonoDevelop.Projects.MSBuild;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuildEditor
{
	delegate IEnumerable<Import> ImportResolver (MSBuildResolveContext resolveContext, string import, DocumentRegion location, Dictionary<string, List<string>> properties);

	class MSBuildResolveContext
	{
		static readonly XName xnProject = new XName ("Project");

		public Dictionary<string, Import> Imports { get; } = new Dictionary<string, Import> (StringComparer.OrdinalIgnoreCase);
		public Dictionary<string, PropertyInfo> Properties { get; } = new Dictionary<string, PropertyInfo> (StringComparer.OrdinalIgnoreCase);
		public Dictionary<string, ItemInfo> Items { get; } = new Dictionary<string, ItemInfo> (StringComparer.OrdinalIgnoreCase);
		public Dictionary<string, TaskInfo> Tasks { get; } = new Dictionary<string, TaskInfo> (StringComparer.OrdinalIgnoreCase);
		public AnnotationTable<XObject, object> Annotations { get; } = new AnnotationTable<XObject, object> ();

		MSBuildResolveContext (string filename)
		{
			Filename = filename;
		}

		public string Filename { get; }

		public static MSBuildResolveContext Create (string filename, XDocument doc, ITextDocument textDocument, ImportResolver resolveImport)
		{
			var ctx = new MSBuildResolveContext (filename);
			var project = doc.Nodes.OfType<XElement> ().FirstOrDefault (x => x.Name == xnProject);
			if (project == null) {
				//TODO: error
				return ctx;
			}

			ctx.ResolveSdks (project, resolveImport);

			var pel = MSBuildElement.Get ("Project");

			var propertiesUsedByImports = GetPropertiesUsedByImports (project);

			//recursively resolve the document
			foreach (var el in project.Elements) {
				ctx.Populate (el, pel, textDocument, propertiesUsedByImports, resolveImport);
			}

			return ctx;
		}

		static Dictionary<string,List<string>> GetPropertiesUsedByImports (XElement project)
		{
			var properties = new Dictionary<string, List<string>> ();
			foreach (var el in project.Elements.Where (el => el.Name.Name == "Import")) {
				var impAtt = el.Attributes.Get (new XName ("Project"), true);
				if (impAtt != null) {
					var expr = new Expression ();
					expr.Parse (impAtt.Value, ParseOptions.None);
					foreach (var prop in expr.Collection.OfType<PropertyReference> ()) {
						properties [prop.Name] = null;
					}
				}
			}
			return properties;
		}

		void ResolveSdks (XElement project, ImportResolver resolveImport)
		{
			var sdksAtt = project.Attributes.Get (new XName ("Sdk"), true);
			if (sdksAtt == null) {
				return;
			}

			string sdks = sdksAtt?.Value;
			if (string.IsNullOrEmpty (sdks)) {
				return;
			}

			foreach (var sdkPath in sdks.Split (new [] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select (s => s.Trim ()).Where (s => s.Length > 0)) {
				var propsPath = $"$(MSBuildSDKsPath)\\{sdkPath}\\Sdk\\Sdk.props";
				var sdkProps = resolveImport (this, propsPath, sdksAtt.Region, null).FirstOrDefault ();
				if (sdkProps != null) {
					Imports.Add (propsPath, sdkProps);
				}

				var targetsPath = $"$(MSBuildSDKsPath)\\{sdkPath}\\Sdk\\Sdk.targets";
				var sdkTargets = resolveImport (this, targetsPath, sdksAtt.Region, null).FirstOrDefault ();
				if (sdkTargets != null) {
					Imports.Add (targetsPath, sdkTargets);
				}
			}
		}

		void Populate (XElement el, MSBuildElement parent, ITextDocument textDocument, Dictionary<string, List<string>> propertiesUsedByImports, ImportResolver resolveImport)
		{
			if (el.Name.Prefix != null)
				return;

			var condition = el.Attributes.Get (new XName ("Condition"), true);
			if (condition != null) {
				ExtractReferences (condition);
			}

			var name = el.Name.Name;

			var msel = MSBuildElement.Get (name, parent);
			if (msel == null)
				return;

			if (!msel.IsSpecial) {
				foreach (var child in el.Nodes.OfType<XElement> ())
					Populate (child, msel, textDocument, propertiesUsedByImports, resolveImport);
			}

			switch (msel.Kind) {
			case MSBuildKind.Import:
				var importAtt = el.Attributes [new XName ("Project")];
				if (importAtt != null) {
					foreach (var import in resolveImport (this, importAtt?.Value, importAtt.Region, propertiesUsedByImports)) {
						Imports [import.Filename] = import;
					}
				}
				return;
			case MSBuildKind.Item:
				ItemInfo item;
				if (!Items.TryGetValue (name, out item))
					Items [name] = item = new ItemInfo (name, null);
				foreach (var metadata in el.Nodes.OfType<XElement> ()) {
					var metaName = metadata.Name.Name;
					if (!metadata.Name.HasPrefix && !item.Metadata.ContainsKey (metaName) && !Builtins.Metadata.ContainsKey (metaName))
						item.Metadata.Add (metaName, new MetadataInfo (metaName, null));
				}
				return;
			case MSBuildKind.Task:
				TaskInfo task;
				if (!Tasks.TryGetValue (name, out task))
					Tasks [name] = task = new TaskInfo (name, null);
				foreach (var att in el.Attributes) {
					if (!att.Name.HasPrefix)
						task.Parameters.Add (att.Name.Name);
					ExtractReferences (att);
				}
				return;
			case MSBuildKind.Property:
				if (!Properties.ContainsKey (name) && !Builtins.Properties.ContainsKey (name)) {
					Properties.Add (name, new PropertyInfo (name, null));
				}
				if (propertiesUsedByImports.TryGetValue (name, out List<string> values) && el.IsClosed && !el.IsSelfClosing) {
					if (values == null) {
						propertiesUsedByImports [name] = values = new List<string> ();
					}
					var val = textDocument.GetTextBetween (el.Region.End, el.ClosingTag.Region.Begin);
					values.Add (val);
				}
				return;
			case MSBuildKind.Target:
				foreach (var att in el.Attributes) {
					switch (att.Name.Name) {
					case "Inputs":
					case "Outputs":
					case "DependOnTargets":
						ExtractReferences (att);
						break;
					}
				}
				return;
			}
		}

		void ExtractReferences (XAttribute att)
		{
			if (!string.IsNullOrEmpty (att.Value))
				ExtractReferences (att.Value);
		}

		void ExtractReferences (string value)
		{
			var expr = new Expression ();
			//TODO: check options
			expr.Parse (value, ExpressionParser.ParseOptions.AllowItemsMetadataAndSplit);

			ExtractReferences (expr);
		}

		void ExtractReferences (Expression expr)
		{
			foreach (var val in expr.Collection) {
				ExtractReferences (val);
			}
		}

		void ExtractReferences (object val)
		{
			//TODO: InvalidExpressionError

			var pr = val as PropertyReference;
			if (pr != null) {
				if (!Properties.ContainsKey (pr.Name) && !Builtins.Properties.ContainsKey (pr.Name)) {
					Properties.Add (pr.Name, new PropertyInfo (pr.Name, null));
				}
				return;
			}

			var ir = val as ItemReference;
			if (ir != null) {
				ItemInfo item;
				if (!Items.TryGetValue (ir.ItemName, out item))
					Items [ir.ItemName] = item = new ItemInfo (ir.ItemName, null);
				if (ir.Transform != null)
					ExtractReferences (ir.Transform);
				return;
			}

			var mr = val as MetadataReference;
			if (mr != null) {
				//TODO: unqualified metadata references
				if (mr.ItemName != null && !Builtins.Metadata.ContainsKey (mr.MetadataName)) {
					ItemInfo item;
					if (!Items.TryGetValue (mr.ItemName, out item))
						Items [mr.ItemName] = item = new ItemInfo (mr.ItemName, null);
					if (!item.Metadata.ContainsKey (mr.MetadataName)) {
						item.Metadata.Add (mr.MetadataName, new MetadataInfo (mr.MetadataName, null));
					}
				}
				return;
			}

			var mir = val as MemberInvocationReference;
			if (mir != null)
				ExtractReferences (mir.Instance);
		}

		public IEnumerable<Import> GetDescendentImports ()
		{
			foreach (var i in Imports) {
				yield return i.Value;
				if (i.Value.ResolveContext != null) {
					foreach (var d in i.Value.ResolveContext.GetDescendentImports ()) {
						yield return d;
					}
				}
			}
		}

		public IEnumerable<MSBuildResolveContext> GetDescendentContexts ()
		{
			foreach (var i in GetDescendentImports ())
				if (i.ResolveContext != null)
					yield return i.ResolveContext;
		}

		public IEnumerable<ItemInfo> GetItems ()
		{
			if (Imports == null)
				return Items.Values;

			var result = new HashSet<ItemInfo> (Items.Values);

			foreach (var child in GetDescendentContexts ()) {
				foreach (var item in child.Items) {
					if (NotPrivate (item.Key)) {
						result.Add (item.Value);
					}
				}
			}

			return result;
		}

		public ItemInfo GetItem (string name)
		{
			return GetAllItemDefinitions (name).FirstOrDefault ();
		}

		IEnumerable<ItemInfo> GetAllItemDefinitions (string name)
		{
			ItemInfo item;
			if (Items.TryGetValue (name, out item))
				yield return item;

			//collect all imports' definitions for this item
			foreach (var child in GetDescendentContexts ()) {
				if (child.Items.TryGetValue (name, out item)) {
					yield return item;
				}
			}
		}

		public IEnumerable<MetadataInfo> GetItemMetadata (string itemName, bool includeBuiltins)
		{
			if (includeBuiltins) {
				foreach (var b in Builtins.Metadata) {
					yield return b.Value;
				}
			}

			var metadataNames = new HashSet<string> ();

			//collect known metadata for this item across all imports
			foreach (var item in GetAllItemDefinitions (itemName)) {
				foreach (var m in item.Metadata) {
					if (metadataNames.Add (m.Key))
						yield return m.Value;
				}
			}
		}

		public IEnumerable<TaskInfo> GetTasks ()
		{
			var result = new HashSet<TaskInfo> (Tasks.Values);

			foreach (var child in GetDescendentContexts ()) {
				foreach (var task in child.Tasks) {
					if (NotPrivate (task.Key)) {
						result.Add (task.Value);
					}
				}
			}

			return result;
		}

		public IEnumerable<TaskInfo> GetTask (string name)
		{
			TaskInfo task;
			if (Tasks.TryGetValue (name, out task))
				yield return task;
			
			foreach (var child in GetDescendentContexts ()) {
				if (child.Tasks.TryGetValue (name, out task)) {
					yield return task;
				}
			}
		}

		public IEnumerable<PropertyInfo> GetProperties (bool includeBuiltins)
		{
			if (includeBuiltins) {
				foreach (var b in Builtins.Properties) {
					yield return b.Value;
				}
			}
			
			var names = new HashSet<string> ();

			foreach (var prop in Properties) {
				names.Add (prop.Key);
				yield return prop.Value;
			}

			foreach (var child in GetDescendentContexts ()) {
				foreach (var prop in child.Properties) {
					if (NotPrivate (prop.Key)) {
						if (names.Add (prop.Key)) {
							yield return prop.Value;
						}
					}
				}
			}
		}

		//by convention, properties and items starting with an underscore are "private"
		static bool NotPrivate (string arg)
		{
			return arg [0] != '_';
		}

		public MSBuildEvaluationContext CreateImportEvalCtx (MSBuildToolsVersion toolsVersion, string projectPath)
		{
			// MSBuildEvaluationContext can only populate these properties fron an MSBuildProject and we don't have one
			// OTOH this isn't a full evaluation anyway. Just set up a bunch of properties commonly used for imports.
			// TODO: add more commonly used properties
			var ctx = new MSBuildEvaluationContext ();

			var runtime = Runtime.SystemAssemblyService.CurrentRuntime;
			string binPath = runtime.GetMSBuildBinPath (toolsVersion.ToVersionString ());
			ctx.SetPropertyValue ("MSBuildBinPath", binPath);
			ctx.SetPropertyValue ("MSBuildToolsPath", binPath);
			var extPath = MSBuildProjectService.ToMSBuildPath (null, runtime.GetMSBuildExtensionsPath ());
			ctx.SetPropertyValue ("MSBuildExtensionsPath", extPath);
			ctx.SetPropertyValue ("MSBuildExtensionsPath32", extPath);
			ctx.SetPropertyValue ("MSBuildProjectDirectory", MSBuildProjectService.ToMSBuildPath (null, Path.GetDirectoryName (projectPath)));
			ctx.SetPropertyValue ("MSBuildThisFileDirectory", MSBuildProjectService.ToMSBuildPath (null, Path.GetDirectoryName (Filename) + Path.DirectorySeparatorChar));

			string sdksPath = Environment.GetEnvironmentVariable ("MSBuildSDKsPath");
			if (sdksPath != null) {
				ctx.SetPropertyValue ("MSBuildSDKsPath", MSBuildProjectService.ToMSBuildPath (null, sdksPath));
			}

			return ctx;
		}
	}
}