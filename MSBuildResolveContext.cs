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
using Microsoft.Build.Framework;
using MonoDevelop.Core;
using MonoDevelop.Ide.Editor;
using MonoDevelop.Ide.TypeSystem;
using MonoDevelop.MSBuildEditor.ExpressionParser;
using MonoDevelop.Xml.Dom;

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
		public List<Error> Errors { get; }

		public MSBuildSdkResolver SdkResolver { get; }
		public bool IsToplevel { get;  }

		MSBuildResolveContext (string filename, bool isToplevel, MSBuildSdkResolver sdkResolver)
		{
			Filename = filename;
			SdkResolver = sdkResolver;
			IsToplevel = isToplevel;

			if (isToplevel) {
				Errors = new List<Error> ();
			}
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

			var resolver = new MSBuildDocumentResolver (ctx, filename, isToplevel, doc, textDocument, sdkResolver, propVals, resolveImport);
			resolver.Run (filename, doc, textDocument);

			ctx.AddSdkTargets (sdks, propVals, resolveImport);

			return ctx;
		}

		static void GetPropertiesUsedByImports (PropertyValueCollector propertyVals, XElement project)
		{
			foreach (var el in project.Elements.Where (el => el.Name.Name == "Import")) {
				var impAtt = el.Attributes.Get (new XName ("Project"), true);
				if (impAtt != null) {
					var expr = new Expression ();
					expr.Parse (impAtt.Value, ExpressionParser.ParseOptions.None);
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
		/// Gets the files in which the given info has been seen.
		/// </summary>
		public IEnumerable<string> GetFilesSeenIn (BaseInfo info)
		{
			var files = new HashSet<string> ();
			if (WasSeen (info)) {
				files.Add (Filename);
			}

			foreach (var child in GetDescendentContexts ()) {
				if (child.WasSeen (info)) {
					files.Add (child.Filename);
				}
			}
			return files;
		}

		bool WasSeen (BaseInfo info)
		{
			if (info is PropertyInfo)
				return Properties.ContainsKey (info.Name);
			if (info is ItemInfo)
				return Items.ContainsKey (info.Name);
			if (info is TaskInfo)
				return Tasks.ContainsKey (info.Name);
			return false;
		}

		//by convention, properties and items starting with an underscore are "private"
		static bool NotPrivate (string arg)
		{
			return arg [0] != '_';
		}
	}
}