// Copyright (c) 2014 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Build.Framework;

using MonoDevelop.MSBuild.Language.Conditions;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Parser;

namespace MonoDevelop.MSBuild.Language
{
	class MSBuildDocument : IMSBuildSchema, IPropertyCollector
	{
		static readonly XName xnProject = new XName ("Project");

		//NOTE: this is keyed on the filepath of resolved imports and original expression of unresolved imports
		//the reason for this is that a single expression can resolve to multiple imports
		public List<Import> Imports { get; } = new List<Import> ();

		public Dictionary<string, PropertyInfo> Properties { get; } = new Dictionary<string, PropertyInfo> (StringComparer.OrdinalIgnoreCase);
		public Dictionary<string, ItemInfo> Items { get; } = new Dictionary<string, ItemInfo> (StringComparer.OrdinalIgnoreCase);
		public Dictionary<string, TaskInfo> Tasks { get; } = new Dictionary<string, TaskInfo> (StringComparer.OrdinalIgnoreCase);
		public Dictionary<string, TargetInfo> Targets { get; } = new Dictionary<string, TargetInfo> (StringComparer.OrdinalIgnoreCase);
		public AnnotationTable<XObject> Annotations { get; } = new AnnotationTable<XObject> ();
		public List<XmlDiagnosticInfo> Errors { get; }
		public bool IsToplevel { get; }

		public HashSet<string> Configurations { get; } = new HashSet<string> ();
		public HashSet<string> Platforms { get; } = new HashSet<string> ();

		public MSBuildDocument (string filename, bool isToplevel)
		{
			Filename = filename;
			IsToplevel = isToplevel;

			if (isToplevel) {
				Errors = new List<XmlDiagnosticInfo> ();
			}
		}

		public string Filename { get; }
		public MSBuildSchema Schema { get; internal set; }

		public void Build (
			XDocument doc, ITextSource textSource,
			MSBuildParserContext context)
		{
			var project = doc.Nodes.OfType<XElement> ().FirstOrDefault (x => x.Name == xnProject);
			if (project == null) {
				//TODO: error
				return;
			}

			var sdks = ResolveSdks (context, project).ToList ();

			var pel = MSBuildLanguageElement.Get ("Project");

			GetPropertiesToTrack (context.PropertyCollector, project);

			var importResolver = context.CreateImportResolver (Filename);

			AddSdkProps (sdks, context.PropertyCollector, importResolver);

			var resolver = new MSBuildSchemaBuilder (IsToplevel, context, importResolver);
			resolver.Run (doc, textSource, this);

			AddSdkTargets (sdks, context.PropertyCollector, importResolver);
		}

		static void GetPropertiesToTrack (PropertyValueCollector propertyVals, XElement project)
		{
			foreach (var el in project.Elements) {
				if (el.NameEquals ("Import", true)) {
					var impAtt = el.Attributes.Get (new XName ("Project"), true);
					if (impAtt != null) {
						MarkProperties (impAtt);
					}
				} else if (el.NameEquals ("UsingTask", true)) {
					var afAtt = el.Attributes.Get (new XName ("AssemblyFile"), true);
					if (afAtt != null) {
						MarkProperties (afAtt);
					}
				}
			}

			void MarkProperties (XAttribute att)
			{
				var expr = ExpressionParser.Parse (att.Value, ExpressionOptions.None);
				foreach (var prop in expr.WithAllDescendants ().OfType<ExpressionProperty> ()) {
					propertyVals.Mark (prop.Name);
				}
			}
		}

		IEnumerable<(string id, TextSpan span)> SplitSdkValue (int offset, string value)
		{
			int start = 0, end;
			while ((end = value.IndexOf (';', start)) > -1) {
				yield return MakeResult ();
				start = end + 1;
			}
			end = value.Length;
			yield return MakeResult ();

			TextSpan CreateSpan (int s, int e) => new TextSpan (offset + s, offset + e);

			(string id, TextSpan loc) MakeResult ()
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
					return (value.Substring (trimStart, trimEnd - trimStart), CreateSpan (trimStart, trimEnd));
				}
				return (null, CreateSpan (start, end));
			}
		}

		IEnumerable<(string id, string path, TextSpan span)> ResolveSdks (MSBuildParserContext context, XElement project)
		{
			var sdksAtt = project.Attributes.Get (new XName ("Sdk"), true);
			if (sdksAtt == null) {
				yield break;
			}

			string sdks = sdksAtt?.Value;
			if (string.IsNullOrEmpty (sdks)) {
				yield break;
			}

			int offset = IsToplevel ? sdksAtt.ValueOffset : sdksAtt.Span.Start;

			foreach (var sdk in SplitSdkValue (offset, sdksAtt.Value)) {
				if (sdk.id == null) {
					if (IsToplevel) {
						Errors.Add (new XmlDiagnosticInfo (DiagnosticSeverity.Warning, "Empty value", sdk.span));
					}
				}
				else {
					var sdkPath = context.GetSdkPath (this, sdk.id, sdk.span);
					if (sdkPath != null) {
						yield return (sdk.id, sdkPath, sdk.span);
					}
					if (IsToplevel) {
						Annotations.Add (sdksAtt, new NavigationAnnotation (sdkPath, sdk.span));
					}
				}
			}
		}

		public virtual void AddImport (Import import)
		{
			Imports.Add (import);
		}

		void AddSdkProps (IEnumerable<(string id, string path, TextSpan loc)> sdkPaths, PropertyValueCollector propVals, MSBuildImportResolver importResolver)
		{
			foreach (var sdk in sdkPaths) {
				var propsPath = $"{sdk.path}\\Sdk.props";
				var sdkProps = importResolver.Resolve (propsPath, sdk.id).FirstOrDefault ();
				if (sdkProps != null) {
					AddImport (sdkProps);
				}
			}
		}

		void AddSdkTargets (IEnumerable<(string id, string path, TextSpan loc)> sdkPaths, PropertyValueCollector propVals, MSBuildImportResolver importResolver)
		{
			foreach (var sdk in sdkPaths) {
				var targetsPath = $"{sdk.path}\\Sdk.targets";
				var sdkTargets = importResolver.Resolve (targetsPath, sdk.id).FirstOrDefault ();
				if (sdkTargets != null) {
					AddImport (sdkTargets);
				}
			}
		}

		public IEnumerable<Import> GetDescendentImports ()
		{
			foreach (var i in Imports) {
				yield return i;
				if (i.Document != null) {
					foreach (var d in i.Document.GetDescendentImports ()) {
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
				if (doc.Filename != null && doc.ContainsInfo (info)) {
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

		public void AddPropertyValues (List<string> combinedProperty, List<string> combinedValue)
		{
			for (int i = 0; i < combinedProperty.Count; i++) {
				var p = combinedProperty [i];
				var v = combinedValue [i];
				switch (p.ToLowerInvariant ()) {
				case "configuration":
					Configurations.Add (v);
					break;
				case "platform":
					Platforms.Add (v);
					break;
				}
			}
		}
	}
}