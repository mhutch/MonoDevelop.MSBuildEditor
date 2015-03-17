//
// MSBuildResolveContext.cs
//
// Author:
//       Michael Hutchinson <mhutch@xamarin.com>
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
using MonoDevelop.Xml.Editor;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Projects.Formats.MSBuild;
using MonoDevelop.Core;
using MonoDevelop.Ide.TypeSystem;
using System.IO;

namespace MonoDevelop.MSBuildEditor
{
	class MSBuildResolveContext
	{
		static readonly XName xnProject = new XName ("Project");

		readonly Dictionary<string,HashSet<string>> items = new Dictionary<string, HashSet<string>> ();
		readonly Dictionary<string,HashSet<string>> tasks = new Dictionary<string, HashSet<string>> ();
		readonly HashSet<string> properties = new HashSet<string> ();
		readonly HashSet<string> imports = new HashSet<string> ();
		public readonly DateTime TimeStampUtc = DateTime.UtcNow;
		string ToolsVersion;

		Dictionary<string,MSBuildResolveContext> resolvedImports;

		MSBuildEvaluationContext importEvalCtx;

		public IEnumerable<string> GetItems ()
		{
			return resolvedImports != null
				? items.Keys.Concat (resolvedImports.Values.SelectMany (i => i.GetItems ())).Distinct ()
				: items.Keys;
		}

		public IEnumerable<string> GetItemMetadata (string itemName)
		{
			HashSet<string> metadata;
			items.TryGetValue (itemName, out metadata);
			if (metadata != null)
				foreach (var m in metadata)
					yield return m;
		}

		public IEnumerable<string> GetTasks ()
		{
			return resolvedImports != null
				? tasks.Keys.Concat (resolvedImports.Values.SelectMany (i => i.GetTasks ())).Distinct ()
				: items.Keys;
		}

		public IEnumerable<string> GetTaskParameters (string taskName)
		{
			HashSet<string> taskParameters;
			items.TryGetValue (taskName, out taskParameters);
			if (taskParameters != null)
				foreach (var parameter in taskParameters)
					yield return parameter;
		}

		public IEnumerable<string> GetProperties ()
		{
			return resolvedImports != null
				? properties.Concat (resolvedImports.Values.SelectMany (i => i.GetProperties ())).Distinct ()
				: properties;
		}

		static string GetToolsVersion (XDocument doc)
		{
			if (doc.RootElement != null) {
				var att = doc.RootElement.Attributes [new XName ("ToolsVersion")];
				if (att != null) {
					var val = att.Value;
					if (!string.IsNullOrEmpty (val))
						return val;
				}
			}
			return "2.0";
		}

		public static MSBuildResolveContext Create (XmlParsedDocument doc, MSBuildResolveContext previous)
		{
			var ctx = new MSBuildResolveContext ();

			ctx.ToolsVersion = GetToolsVersion(doc.XDocument);
			if (string.IsNullOrEmpty (ctx.ToolsVersion)) {
				ctx.ToolsVersion = "2.0";
			}

			if (previous != null && previous.ToolsVersion == ctx.ToolsVersion) {
				ctx.importEvalCtx = previous.importEvalCtx;
			} else {
				ctx.importEvalCtx = CreateImportEvalCtx (ctx.ToolsVersion);
			}

			ctx.Populate (doc.XDocument);

			if (previous != null && doc.Errors.Count > 0)
				ctx.Merge (previous);

			ctx.resolvedImports = new Dictionary<string, MSBuildResolveContext> ();
			if (previous != null && previous.ToolsVersion == ctx.ToolsVersion && previous.resolvedImports != null) {
				ctx.ResolveImports (ctx.imports, previous);
			} else {
				ctx.ResolveImports (ctx.imports, null);
			}

			return ctx;
		}

		void ResolveImports (HashSet<string> imports, MSBuildResolveContext previous, string basePath = null)
		{
			foreach (var import in imports) {
				string filename;
				if (!importEvalCtx.Evaluate (import, out filename)) {
					continue;
				}

				if (basePath != null) {
					filename = Path.Combine (basePath, filename);
				}

				if (!File.Exists (filename)) {
					continue;
				}

				if (previous != null && previous.ToolsVersion == ToolsVersion && previous.resolvedImports != null) {
					MSBuildResolveContext prevImport;
					//ignore mtimes on imports for now // && prevImport.TimeStampUtc <= File.GetLastWriteTimeUtc (filename)
					if (previous.resolvedImports.TryGetValue (filename, out prevImport)) {
						resolvedImports [filename] = prevImport;
						continue;
					}
				}

				if (resolvedImports.ContainsKey (filename)) {
					continue;
				}

				var doc = (XmlParsedDocument) TypeSystemService.ParseFile (filename, "application/xml", File.ReadAllText (filename));
				var ctx = new MSBuildResolveContext ();
				if (doc.XDocument != null) {
					ctx.Populate (doc.XDocument);
				}
				resolvedImports [filename] = ctx;
				var bp = Path.GetDirectoryName (filename);
				ResolveImports (ctx.imports, previous, bp);
			}
		}

		void Merge (MSBuildResolveContext other)
		{
			foreach (var otherItem in other.items) {
				HashSet<string> item;
				if (items.TryGetValue (otherItem.Key, out item)) {
					foreach (var att in otherItem.Value)
						item.Add (att);
				} else {
					items [otherItem.Key] = otherItem.Value;
				}
			}
			foreach (var otherTask in other.tasks) {
				HashSet<string> task;
				if (tasks.TryGetValue (otherTask.Key, out task)) {
					foreach (var att in otherTask.Value)
						task.Add (att);
				} else {
					tasks [otherTask.Key] = otherTask.Value;
				}
			}
			foreach (var prop in other.properties) {
				properties.Add (prop);
			}
			foreach (var imp in other.imports) {
				imports.Add (imp);
			}
		}

		void Populate (XDocument doc)
		{
			var project = doc.Nodes.OfType<XElement> ().FirstOrDefault (x => x.Name == xnProject);
			if (project == null)
				return;
			var pel = MSBuildElement.Get ("Project");
			foreach (var el in project.Nodes.OfType<XElement> ())
				Populate (el, pel);
		}

		void Populate (XElement el, MSBuildElement parent)
		{
			if (el.Name.Prefix != null)
				return;

			var name = el.Name.Name;

			var msel = MSBuildElement.Get (name, parent);
			if (msel == null)
				return;

			if (!msel.IsSpecial) {
				foreach (var child in el.Nodes.OfType<XElement> ())
					Populate (child, msel);
			}

			switch (msel.Name) {
			case "Import":
				var import = el.Attributes [xnProject];
				if (import != null && !string.IsNullOrEmpty (import.Value)) {
					imports.Add (import.Value);
				}
				return;
			case "Item":
				HashSet<string> item;
				if (!items.TryGetValue (name, out item))
					items [name] = item = new HashSet<string> ();
				foreach (var metadata in el.Nodes.OfType<XElement> ())
					if (!metadata.Name.HasPrefix)
						item.Add (metadata.Name.Name);
				return;
			case "Task":
				HashSet<string> task;
				if (!tasks.TryGetValue (name, out task))
					tasks [name] = task = new HashSet<string> ();
				foreach (var att in el.Attributes)
					if (!att.Name.HasPrefix)
						task.Add (att.Name.Name);
				return;
			case "Property":
				properties.Add (name);
				return;
			}
		}

		static MSBuildEvaluationContext CreateImportEvalCtx (string toolsVersion)
		{
			var ctx = new MSBuildEvaluationContext ();
			var runtime = Runtime.SystemAssemblyService.CurrentRuntime;
			ctx.SetPropertyValue ("MSBuildBinPath", runtime.GetMSBuildBinPath (toolsVersion));
			var extPath = runtime.GetMSBuildExtensionsPath ();
			ctx.SetPropertyValue ("MSBuildExtensionsPath", extPath);
			ctx.SetPropertyValue ("MSBuildExtensionsPath32", extPath);
			return ctx;
		}
	}
}