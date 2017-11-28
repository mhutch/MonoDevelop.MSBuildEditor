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
using MonoDevelop.MSBuildEditor.Schema;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuildEditor.Language
{
	delegate IEnumerable<Import> ImportResolver (string import, string sdk, PropertyValueCollector propertyVals);

	class MSBuildDocument : IMSBuildSchema
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

		public MSBuildDocument (string filename, bool isToplevel)
		{
			Filename = filename;
			IsToplevel = isToplevel;

			if (isToplevel) {
				Errors = new List<Error> ();
			}
		}

		public string Filename { get; }
		public MSBuildSchema Schema { get; internal set; }

		public void Build (
			XDocument doc, ITextDocument textDocument,
			IRuntimeInformation runtime, PropertyValueCollector propVals,
			ImportResolver resolveImport)
		{
			var project = doc.Nodes.OfType<XElement> ().FirstOrDefault (x => x.Name == xnProject);
			if (project == null) {
				//TODO: error
				return;
			}

			var sdks = ResolveSdks (runtime, project, textDocument).ToList ();

			var pel = MSBuildLanguageElement.Get ("Project");

			GetPropertiesUsedByImports (propVals, project);

			AddSdkProps (sdks, propVals, resolveImport);

			var resolver = new MSBuildSchemaBuilder (IsToplevel, runtime, propVals, resolveImport);
			resolver.Run (doc, Filename, textDocument, this);

			AddSdkTargets (sdks, propVals, resolveImport);
		}

		static void GetPropertiesUsedByImports (PropertyValueCollector propertyVals, XElement project)
		{
			foreach (var el in project.Elements.Where (el => el.Name.Name == "Import")) {
				var impAtt = el.Attributes.Get (new XName ("Project"), true);
				if (impAtt != null) {
					var expr = ExpressionParser.Parse (impAtt.Value, ExpressionOptions.None);
					foreach (var prop in expr.WithAllDescendants ().OfType<ExpressionProperty> ()) {
						propertyVals.Mark (prop.Name);
					}
				}
			}
		}

		internal string GetSdkPath (IRuntimeInformation runtime, string sdk, DocumentRegion loc)
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
			var sdkPath = runtime.GetSdkPath (sdkRef, Filename, null);
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

		IEnumerable<(string id, string path, DocumentRegion)> ResolveSdks (IRuntimeInformation runtime, XElement project, ITextDocument doc)
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
					var sdkPath = GetSdkPath (runtime, sdk.id, sdk.loc);
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
				var sdkProps = resolveImport (propsPath, sdk.id, propVals).FirstOrDefault ();
				if (sdkProps != null) {
					Imports.Add (sdkProps.Filename, sdkProps);
				}
			}
		}

		void AddSdkTargets (IEnumerable<(string id, string path, DocumentRegion loc)> sdkPaths, PropertyValueCollector propVals, ImportResolver resolveImport)
		{
			foreach (var sdk in sdkPaths) {
				var targetsPath = $"{sdk.path}\\Sdk.targets";
				var sdkTargets = resolveImport (targetsPath, sdk.id, propVals).FirstOrDefault ();
				if (sdkTargets != null) {
					Imports.Add (sdkTargets.Filename, sdkTargets);
				}
			}
		}

		public IEnumerable<Import> GetDescendentImports ()
		{
			foreach (var i in Imports) {
				yield return i.Value;
				if (i.Value.Document != null) {
					foreach (var d in i.Value.Document.GetDescendentImports ()) {
						yield return d;
					}
				}
			}
		}

		IEnumerable<MSBuildDocument> GetDescendentDocuments ()
		{
			foreach (var i in GetDescendentImports ()) {
				if (i.Document != null) {
					yield return i.Document;
				}
			}
		}

		public IEnumerable<MSBuildDocument> GetSelfAndDescendents ()
		{
			yield return this;
			foreach (var i in GetDescendentImports ()) {
				if (i.Document != null) {
					yield return i.Document;
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
				if (i.Document?.Schema != null)
					yield return i.Document.Schema;
			}
			yield return this;
			foreach (var i in GetDescendentImports ()) {
				if (i.Document != null) {
					yield return i.Document;
				}
			}
		}

		/// <summary>
		/// Gets the files in which the given info has been seen, excluding the current one.
		/// </summary>
		public IEnumerable<string> GetFilesSeenIn (BaseInfo info)
		{
			var files = new HashSet<string> ();
			foreach (var doc in GetDescendentDocuments ()) {
				if (doc.ContainsInfo (info)) {
					files.Add (doc.Filename);
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