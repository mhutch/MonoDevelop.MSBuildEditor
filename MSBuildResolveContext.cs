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
using System.Linq;
using MonoDevelop.Core;
using MonoDevelop.Ide.Editor;
using MonoDevelop.MSBuildEditor.ExpressionParser;
using MonoDevelop.Xml.Dom;
using Microsoft.Build.Framework;

namespace MonoDevelop.MSBuildEditor
{
	delegate IEnumerable<Import> ImportResolver (MSBuildResolveContext resolveContext, string import, string sdk, DocumentRegion importAttLocation, DocumentRegion sdkAttLocation, PropertyValueCollector propertyVals);

	class MSBuildResolveContext
	{
		static readonly XName xnProject = new XName ("Project");

		public Dictionary<string, Import> Imports { get; } = new Dictionary<string, Import> (StringComparer.OrdinalIgnoreCase);
		public Dictionary<string, PropertyInfo> Properties { get; } = new Dictionary<string, PropertyInfo> (StringComparer.OrdinalIgnoreCase);
		public Dictionary<string, ItemInfo> Items { get; } = new Dictionary<string, ItemInfo> (StringComparer.OrdinalIgnoreCase);
		public Dictionary<string, TaskInfo> Tasks { get; } = new Dictionary<string, TaskInfo> (StringComparer.OrdinalIgnoreCase);
		public AnnotationTable<XObject> Annotations { get; } = new AnnotationTable<XObject> ();

		public MSBuildSdkResolver SdkResolver { get; }
		public bool IsToplevel { get;  }

		MSBuildResolveContext (string filename, bool isToplevel, MSBuildSdkResolver sdkResolver)
		{
			Filename = filename;
			SdkResolver = sdkResolver;
			IsToplevel = isToplevel;
		}

		public string Filename { get; }

		public static MSBuildResolveContext Create (string filename, bool isToplevel, XDocument doc, ITextDocument textDocument, MSBuildSdkResolver sdkResolver, PropertyValueCollector propVals, ImportResolver resolveImport)
		{
			var ctx = new MSBuildResolveContext (filename, isToplevel, sdkResolver);
			var project = doc.Nodes.OfType<XElement> ().FirstOrDefault (x => x.Name == xnProject);
			if (project == null) {
				//TODO: error
				return ctx;
			}

			var sdks = ctx.ResolveSdks (project).ToList ();

			var pel = MSBuildElement.Get ("Project");

			GetPropertiesUsedByImports (propVals, project);

			ctx.AddSdkProps (sdks, propVals, resolveImport);

			//recursively resolve the document
			foreach (var el in project.Elements) {
				ctx.Populate (el, pel, textDocument, propVals, resolveImport);
			}

			ctx.AddSdkTargets (sdks, propVals, resolveImport);

			return ctx;
		}

		static void GetPropertiesUsedByImports (PropertyValueCollector propertyVals, XElement project)
		{
			foreach (var el in project.Elements.Where (el => el.Name.Name == "Import")) {
				var impAtt = el.Attributes.Get (new XName ("Project"), true);
				if (impAtt != null) {
					var expr = new Expression ();
					expr.Parse (impAtt.Value, ParseOptions.None);
					foreach (var prop in expr.Collection.OfType<PropertyReference> ()) {
						propertyVals.Mark (prop.Name);
					}
				}
			}
		}

		string GetSdkPath (string sdk)
		{
			if (!SdkReference.TryParse (sdk, out SdkReference sdkRef)) {
				//TODO: squiggle the SDK
				LoggingService.LogError ($"Could not parse SDK {sdk}");
				return null;
			}

			//FIXME: filename should be the root project, not this file
			var sdkPath = SdkResolver.GetSdkPath (sdkRef, Filename, null);
			if (sdkPath == null) {
				//TODO: squiggle the SDK
				LoggingService.LogError ($"Did not find SDK {sdk}");
				return null;
			}

			return sdkPath;
		}

		IEnumerable<(string,DocumentRegion)> ResolveSdks (XElement project)
		{
			var sdksAtt = project.Attributes.Get (new XName ("Sdk"), true);
			if (sdksAtt == null) {
				yield break;
			}

			string sdks = sdksAtt?.Value;
			if (string.IsNullOrEmpty (sdks)) {
				yield break;
			}

			//FIXME pin the regions down a little more
			foreach (var sdk in sdks.Split (new [] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select (s => s.Trim ()).Where (s => s.Length > 0)) {
				var sdkPath = GetSdkPath (sdk);
				if (sdkPath != null) {
					yield return (sdkPath, sdksAtt.Region);
				}
			}
		}

		void AddSdkProps(IEnumerable<(string, DocumentRegion)> sdkPaths, PropertyValueCollector propVals, ImportResolver resolveImport)
		{
			foreach (var (sdkPath, sdkLoc) in sdkPaths) {
				var propsPath = $"{sdkPath}\\Sdk.props";
				var sdkProps = resolveImport (this, propsPath, null, sdkLoc, DocumentRegion.Empty, propVals).FirstOrDefault ();
				if (sdkProps != null) {
					Imports.Add (propsPath, sdkProps);
				}
			}
		}

		void AddSdkTargets(IEnumerable<(string, DocumentRegion)> sdkPaths, PropertyValueCollector propVals, ImportResolver resolveImport)
		{
			foreach (var (sdkPath, sdkLoc) in sdkPaths) {
				var targetsPath = $"{sdkPath}\\Sdk.targets";
				var sdkTargets = resolveImport (this, targetsPath, null, sdkLoc, DocumentRegion.Empty, propVals).FirstOrDefault ();
				if (sdkTargets != null) {
					Imports.Add (targetsPath, sdkTargets);
				}
			}
		}

		void Populate (XElement el, MSBuildElement parent, ITextDocument textDocument, PropertyValueCollector propertyVals, ImportResolver resolveImport)
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
					Populate (child, msel, textDocument, propertyVals, resolveImport);
			}

			switch (msel.Kind) {
			case MSBuildKind.Import:
				var importAtt = el.Attributes [new XName ("Project")];
				var sdkAtt = el.Attributes [new XName ("Sdk")];
				if (importAtt != null) {
					foreach (var import in resolveImport (this, importAtt.Value, sdkAtt?.Value, importAtt.Region, sdkAtt?.Region ?? DocumentRegion.Empty, propertyVals)) {
						Imports [import.Filename] = import;
						if (IsToplevel) {
							Annotations.Add (importAtt, import);
						}
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
				propertyVals.Collect (name, el, textDocument);
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

			if (val is PropertyReference pr) {
				if (!Properties.ContainsKey (pr.Name) && !Builtins.Properties.ContainsKey (pr.Name)) {
					Properties.Add (pr.Name, new PropertyInfo (pr.Name, null));
				}
				return;
			}

			if (val is ItemReference ir) {
				if (!Items.TryGetValue (ir.ItemName, out ItemInfo item))
					Items [ir.ItemName] = item = new ItemInfo (ir.ItemName, null);
				if (ir.Transform != null)
					ExtractReferences (ir.Transform);
				return;
			}

			if (val is MetadataReference mr) {
				//TODO: unqualified metadata references
				if (mr.ItemName != null && !Builtins.Metadata.ContainsKey (mr.MetadataName)) {
					if (!Items.TryGetValue (mr.ItemName, out ItemInfo item))
						Items [mr.ItemName] = item = new ItemInfo (mr.ItemName, null);
					if (!item.Metadata.ContainsKey (mr.MetadataName)) {
						item.Metadata.Add (mr.MetadataName, new MetadataInfo (mr.MetadataName, null));
					}
				}
				return;
			}

			if (val is MemberInvocationReference mir)
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
			if (Items.TryGetValue (name, out ItemInfo item))
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

			//special case some well known metadata
			//TODO: make this a schema or something
			if (itemName == "PackageReference") {
				if (metadataNames.Add ("Version"))
					yield return new MetadataInfo ("Version", "The version of the package");
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
			if (Tasks.TryGetValue (name, out TaskInfo task))
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

		/// <summary>
		/// Gets the files in which the given property has been seen.
		/// </summary>
		public IEnumerable<string> GetFilesPropertySeenIn (string name)
		{
			if (Properties.ContainsKey (name)) {
				yield return Filename;
			}

			foreach (var child in GetDescendentContexts ()) {
				if (child.Properties.ContainsKey (name)) {
					yield return child.Filename;
				}
			}
		}

		//by convention, properties and items starting with an underscore are "private"
		static bool NotPrivate (string arg)
		{
			return arg [0] != '_';
		}
	}
}