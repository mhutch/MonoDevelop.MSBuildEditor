// Copyright (c) 2014 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using MonoDevelop.Core;
using MonoDevelop.Ide.Editor;
using MonoDevelop.Ide.TypeSystem;
using MonoDevelop.MSBuildEditor.ExpressionParser;
using MonoDevelop.MSBuildEditor.Schema;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuildEditor.Language
{
	delegate IEnumerable<Import> ImportResolver (MSBuildResolveContext resolveContext, string import, string sdk, PropertyValueCollector propertyVals);

	class MSBuildResolveContext : IMSBuildSchema
	{
		static readonly XName xnProject = new XName ("Project");

		public Dictionary<string, Import> Imports { get; } = new Dictionary<string, Import> (StringComparer.OrdinalIgnoreCase);
		public Dictionary<string, PropertyInfo> Properties { get; } = new Dictionary<string, PropertyInfo> (StringComparer.OrdinalIgnoreCase);
		public Dictionary<string, ItemInfo> Items { get; } = new Dictionary<string, ItemInfo> (StringComparer.OrdinalIgnoreCase);
		public Dictionary<string, TaskInfo> Tasks { get; } = new Dictionary<string, TaskInfo> (StringComparer.OrdinalIgnoreCase);
		public Dictionary<string, TargetInfo> Targets { get; } = new Dictionary<string, TargetInfo> (StringComparer.OrdinalIgnoreCase);
		public AnnotationTable<XObject> Annotations { get; } = new AnnotationTable<XObject> ();
		public List<Error> Errors { get; }
		public bool IsToplevel { get; }

		MSBuildResolveContext (string filename, bool isToplevel)
		{
			Filename = filename;
			IsToplevel = isToplevel;

			if (isToplevel) {
				Errors = new List<Error> ();
			}
		}

		public string Filename { get; }
		public MSBuildSchema Schema { get; internal set; }

		public static MSBuildResolveContext Create (
			string filename, bool isToplevel, XDocument doc, ITextDocument textDocument,
			MSBuildSdkResolver sdkResolver, PropertyValueCollector propVals,
			ImportResolver resolveImport)
		{
			var ctx = new MSBuildResolveContext (filename, isToplevel);
			var project = doc.Nodes.OfType<XElement> ().FirstOrDefault (x => x.Name == xnProject);
			if (project == null) {
				//TODO: error
				return ctx;
			}

			var sdks = ctx.ResolveSdks (sdkResolver, project, textDocument).ToList ();

			var pel = MSBuildLanguageElement.Get ("Project");

			GetPropertiesUsedByImports (propVals, project);

			ctx.AddSdkProps (sdks, propVals, resolveImport);

			MSBuildDocumentResolver resolver;
			if (isToplevel) {
				resolver = new ValidatingDocumentResolver (ctx, filename, isToplevel, textDocument, sdkResolver, propVals, resolveImport);
			} else {
				resolver = new MSBuildDocumentResolver (ctx, filename, isToplevel, textDocument, sdkResolver, propVals, resolveImport);
			}
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

		internal string GetSdkPath (MSBuildSdkResolver resolver, string sdk, DocumentRegion loc)
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
			var sdkPath = resolver.GetSdkPath (sdkRef, Filename, null);
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
					if (!char.IsWhiteSpace (value[trimStart]))
						break;
					trimStart++;
				}
				while (trimEnd > trimStart) {
					if (!char.IsWhiteSpace (value[trimEnd - 1]))
						break;
					trimEnd--;
				}
				if (trimEnd > trimStart) {
					return (value.Substring (trimStart, trimEnd - trimStart), Region (trimStart, trimEnd));
				}
				return (null, Region (start, end));
			}
		}

		IEnumerable<(string id, string path, DocumentRegion)> ResolveSdks (MSBuildSdkResolver resolver, XElement project, ITextDocument doc)
		{
			var sdksAtt = project.Attributes.Get (new XName ("Sdk"), true);
			if (sdksAtt == null) {
				yield break;
			}

			string sdks = sdksAtt?.Value;
			if (string.IsNullOrEmpty (sdks)) {
				yield break;
			}

			DocumentLocation start = IsToplevel ? sdksAtt.GetValueStart (doc) : sdksAtt.Region.Begin;

			foreach (var sdk in SplitSdkValue (start, sdksAtt.Value)) {
				if (sdk.id == null) {
					if (IsToplevel) {
						Errors.Add (new Error (ErrorType.Warning, "Empty value", sdk.loc));
					}
				}
				else {
					var sdkPath = GetSdkPath (resolver, sdk.id, sdk.loc);
					if (sdkPath != null) {
						yield return (sdk.id, sdkPath, sdk.loc);
					}
					if (IsToplevel) {
						Annotations.Add (sdksAtt, new NavigationAnnotation (sdkPath, sdk.loc));
					}
				}
			}
		}

		void AddSdkProps (IEnumerable<(string id, string path, DocumentRegion loc)> sdkPaths, PropertyValueCollector propVals, ImportResolver resolveImport)
		{
			foreach (var sdk in sdkPaths) {
				var propsPath = $"{sdk.path}\\Sdk.props";
				var sdkProps = resolveImport (this, propsPath, sdk.id, propVals).FirstOrDefault ();
				if (sdkProps != null) {
					Imports.Add (propsPath, sdkProps);
				}
			}
		}

		void AddSdkTargets (IEnumerable<(string id, string path, DocumentRegion loc)> sdkPaths, PropertyValueCollector propVals, ImportResolver resolveImport)
		{
			foreach (var sdk in sdkPaths) {
				var targetsPath = $"{sdk.path}\\Sdk.targets";
				var sdkTargets = resolveImport (this, targetsPath, sdk.id, propVals).FirstOrDefault ();
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

		IEnumerable<MSBuildResolveContext> GetDescendentContexts ()
		{
			foreach (var i in GetDescendentImports ()) {
				if (i.ResolveContext != null) {
					yield return i.ResolveContext;
				}
			}
		}

		public IEnumerable<MSBuildResolveContext> GetContextAndDescendents ()
		{
			yield return this;
			foreach (var i in GetDescendentImports ()) {
				if (i.ResolveContext != null) {
					yield return i.ResolveContext;
				}
			}
		}

		//actual schemas, if they exist, take precedence over inferred schemas
		public IEnumerable<IMSBuildSchema> GetSchemas ()
		{
			if (Schema != null) {
				yield return Schema;
			}
			foreach (var i in GetDescendentImports ()) {
				if (i.ResolveContext?.Schema != null)
					yield return i.ResolveContext.Schema;
			}
			yield return this;
			foreach (var i in GetDescendentImports ()) {
				if (i.ResolveContext != null) {
					yield return i.ResolveContext;
				}
			}
		}

		/// <summary>
		/// Gets the files in which the given info has been seen, excluding the current one.
		/// </summary>
		public IEnumerable<string> GetFilesSeenIn (BaseInfo info)
		{
			var files = new HashSet<string> ();
			foreach (var ctx in GetDescendentContexts ()) {
				if (ctx.ContainsInfo (info)) {
					files.Add (ctx.Filename);
				}
			}
			return files;
		}

		public bool ContainsInfo (BaseInfo info)
		{
			if (info is PropertyInfo)
				return Properties.ContainsKey (info.Name);
			if (info is ItemInfo)
				return Items.ContainsKey (info.Name);
			if (info is TaskInfo)
				return Tasks.ContainsKey (info.Name);
			if (info is TargetInfo)
				return Targets.ContainsKey (info.Name);
			return false;
		}

		public bool IsPrivate (string name)
		{
			//properties and items are always visible from files they're used in
			return !IsToplevel && name[0] == '_';
		}
	}
}