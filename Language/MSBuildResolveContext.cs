// Copyright (c) 2014 Xamarin Inc.
// Copyright (c) Microsoft. ALl rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using MonoDevelop.Core;
using MonoDevelop.Ide.Editor;
using MonoDevelop.Ide.TypeSystem;
using MonoDevelop.MSBuildEditor.ExpressionParser;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuildEditor.Language
{
	delegate IEnumerable<Import> ImportResolver (MSBuildResolveContext resolveContext, string import, PropertyValueCollector propertyVals);

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

			var sdks = ctx.ResolveSdks (project, textDocument).ToList ();

			var pel = MSBuildSchemaElement.Get ("Project");

			GetPropertiesUsedByImports (propVals, project);

			ctx.AddSdkProps (sdks, propVals, resolveImport);

			var resolver = new MSBuildDocumentResolver (ctx, filename, isToplevel, doc, textDocument, sdkResolver, propVals, resolveImport);
			resolver.Run (filename, textDocument, doc);

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

		internal string GetSdkPath (string sdk, DocumentRegion loc)
		{
			if (!SdkReference.TryParse (sdk, out SdkReference sdkRef)) {
				string message = $"Could not parse SDK '{sdk}'";
				LoggingService.LogError (message);
				if (IsToplevel) {
					AddError (message);
				}
				return null;
			}

			//FIXME: filename should be the root project, not this file
			var sdkPath = SdkResolver.GetSdkPath (sdkRef, Filename, null);
			if (sdkPath == null) {
				string message = $"Did not find SDK '{sdk}'";
				LoggingService.LogError (message);
				if (IsToplevel) {
					AddError (message);
				}
				return null;
			}

			return sdkPath;

			void AddError (string msg) => Errors.Add (new Error (ErrorType.Error, msg, loc));
		}

		IEnumerable<(string id, DocumentRegion loc)> SplitSdkValue (DocumentLocation location, string value)
		{
			int start = 0, end;
			while ((end = value.IndexOf (';', start)) > -1) {
				yield return MakeResult ();
				start = end + 1;
			}
			end = value.Length;
			yield return MakeResult ();

			DocumentRegion Region (int s, int e) => new DocumentRegion (location.Line, location.Column + s, location.Line, location.Column + e);

			(string id, DocumentRegion loc) MakeResult ()
			{
				int trimStart = start, trimEnd = end;
				while (trimStart < trimEnd) {
					if (!char.IsWhiteSpace (value [trimStart]))
						break;
					trimStart++;
				}
				while (trimEnd > trimStart) {
					if (!char.IsWhiteSpace (value [trimEnd - 1]))
						break;
					trimEnd--;
				}
				if (trimEnd > trimStart) {
					return (value.Substring (trimStart, trimEnd - trimStart), Region (trimStart,trimEnd));
				}
				return (null, Region (start, end));
			}
		}

		IEnumerable<(string, DocumentRegion)> ResolveSdks (XElement project, ITextDocument doc)
		{
			var sdksAtt = project.Attributes.Get (new XName ("Sdk"), true);
			if (sdksAtt == null) {
				yield break;
			}

			string sdks = sdksAtt?.Value;
			if (string.IsNullOrEmpty (sdks)) {
				yield break;
			}

			DocumentLocation start = IsToplevel? sdksAtt.GetValueStart (doc) : sdksAtt.Region.Begin;

			foreach (var sdk in SplitSdkValue (start, sdksAtt.Value)) {
				if (sdk.id == null) {
					if (IsToplevel) {
						Errors.Add (new Error (ErrorType.Warning, "Empty value", sdk.loc));
					}
				} else {
					var sdkPath = GetSdkPath (sdk.id, sdk.loc);
					if (sdkPath != null) {
						yield return (sdkPath, sdk.loc);
					}
					if (IsToplevel) {
						Annotations.Add (sdksAtt, new NavigationAnnotation (sdkPath, sdk.loc));
					}
				}
			}
		}

		void AddSdkProps(IEnumerable<(string, DocumentRegion)> sdkPaths, PropertyValueCollector propVals, ImportResolver resolveImport)
		{
			foreach (var (sdkPath, sdkLoc) in sdkPaths) {
				var propsPath = $"{sdkPath}\\Sdk.props";
				var sdkProps = resolveImport (this, propsPath, propVals).FirstOrDefault ();
				if (sdkProps != null) {
					Imports.Add (propsPath, sdkProps);
				}
			}
		}

		void AddSdkTargets(IEnumerable<(string, DocumentRegion)> sdkPaths, PropertyValueCollector propVals, ImportResolver resolveImport)
		{
			foreach (var (sdkPath, sdkLoc) in sdkPaths) {
				var targetsPath = $"{sdkPath}\\Sdk.targets";
				var sdkTargets = resolveImport (this, targetsPath, propVals).FirstOrDefault ();
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