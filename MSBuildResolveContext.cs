//
// MSBuildResolveContext.cs
//
// Author:
//       mhutch <m.j.hutchinson@gmail.com>
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
using System.Threading.Tasks;

using MonoDevelop.Core;
using MonoDevelop.Ide;
using MonoDevelop.Ide.TypeSystem;
using MonoDevelop.Projects.Formats.MSBuild;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Editor;
using MonoDevelop.MSBuildEditor.ExpressionParser;

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
			if (resolvedImports == null)
				return items.Keys;

			var result = new HashSet<string> (items.Keys);

			foreach (var import in resolvedImports)
				foreach (var val in import.Value.GetItems ())
					if (NotPrivate (val))
						result.Add (val);

			return result;
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

		public HashSet<string> GetTask (string name)
		{
			if (resolvedImports != null) {
				foreach (var import in resolvedImports.Values) {
					var t = import.GetTask (name);
					if (t != null)
						return t;
				}
			}

			HashSet<string> v;
			tasks.TryGetValue (name, out v);
			return v;
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
			if (resolvedImports == null)
				return properties;

			return properties
				.Concat (resolvedImports.Values.SelectMany (i => i.GetProperties ()).Where (NotPrivate))
				.Distinct ();
		}

		//by convention, properties and items starting with an underscore are "private"
		static bool NotPrivate (string arg)
		{
			return arg [0] != '_';
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

		public static async Task<MSBuildResolveContext> Create (XmlParsedDocument doc, MSBuildResolveContext previous)
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

			if (previous != null && doc.HasErrors)
				ctx.Merge (previous);

			ctx.resolvedImports = new Dictionary<string, MSBuildResolveContext> ();
			if (previous != null && previous.ToolsVersion == ctx.ToolsVersion && previous.resolvedImports != null) {
				await ctx.ResolveImports (ctx.imports, previous);
			} else {
				await ctx.ResolveImports (ctx.imports, null);
			}

			return ctx;
		}

		async Task ResolveImports (HashSet<string> imports, MSBuildResolveContext previous, string basePath = null)
		{
			foreach (var import in imports) {
				string filename = importEvalCtx.Evaluate (import);

				if (basePath != null) {
					filename = Path.Combine (basePath, filename);
				}

				if (!Platform.IsWindows) {
					filename = filename.Replace ('\\', '/');
				}

				if (!File.Exists (filename)) {
					continue;
				}

				var bp = Path.GetDirectoryName (filename);

				if (previous != null && previous.ToolsVersion == ToolsVersion && previous.resolvedImports != null) {
					MSBuildResolveContext prevImport;
					//ignore mtimes on imports for now // && prevImport.TimeStampUtc <= File.GetLastWriteTimeUtc (filename)
					if (previous.resolvedImports.TryGetValue (filename, out prevImport)) {
						resolvedImports [filename] = prevImport;
						await ResolveImports (prevImport.imports, previous, bp);
						continue;
					}
				}

				if (resolvedImports.ContainsKey (filename)) {
					continue;
				}

				var parseOptions = new Ide.TypeSystem.ParseOptions {
					FileName = filename,
					Content = TextFileProvider.Instance.GetReadOnlyTextEditorData (filename)
				};
				var doc = (XmlParsedDocument)await TypeSystemService.ParseFile (parseOptions, "application/xml");
				var ctx = new MSBuildResolveContext ();
				if (doc.XDocument != null) {
					ctx.Populate (doc.XDocument);
				}
				resolvedImports [filename] = ctx;
				await ResolveImports (ctx.imports, previous, bp);
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
					Populate (child, msel);
			}

			switch (msel.Kind) {
			case MSBuildKind.Import:
				var import = el.Attributes [xnProject];
				if (import != null && !string.IsNullOrEmpty (import.Value)) {
					imports.Add (import.Value);
				}
				return;
			case MSBuildKind.Item:
				HashSet<string> item;
				Console.WriteLine (name);
				if (!items.TryGetValue (name, out item))
					items [name] = item = new HashSet<string> ();
				foreach (var metadata in el.Nodes.OfType<XElement> ())
					if (!metadata.Name.HasPrefix)
						item.Add (metadata.Name.Name);
				return;
			case MSBuildKind.Task:
				HashSet<string> task;
				if (!tasks.TryGetValue (name, out task))
					tasks [name] = task = new HashSet<string> ();
				foreach (var att in el.Attributes) {
					if (!att.Name.HasPrefix)
						task.Add (att.Name.Name);
					ExtractReferences (att);
				}
				return;
			case MSBuildKind.Property:
				properties.Add (name);
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
				properties.Add (pr.Name);
				return;
			}

			var ir = val as ItemReference;
			if (ir != null) {
				HashSet<string> item;
				if (!items.TryGetValue (ir.ItemName, out item))
					items [ir.ItemName] = item = new HashSet<string> ();
				if (ir.Transform != null)
					ExtractReferences (ir.Transform);
				return;
			}

			var mr = val as MetadataReference;
			if (mr != null) {
				//TODO: unqualified metadata references
				if (mr.ItemName != null) {
					HashSet<string> item;
					if (!items.TryGetValue (mr.ItemName, out item))
						items [mr.ItemName] = item = new HashSet<string> ();
					item.Add (mr.MetadataName);
				}
				return;
			}

			var mir = val as MemberInvocationReference;
			if (mir != null)
				ExtractReferences (mir.Instance);
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