// Copyright (c) 2014 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;

using MonoDevelop.MSBuild.Analysis;
using MonoDevelop.MSBuild.Dom;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.MSBuild.Language.Typesystem;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Parser;
using MonoDevelop.MSBuild.SdkResolution;
using MonoDevelop.MSBuild.Evaluation;

namespace MonoDevelop.MSBuild.Language
{
	class MSBuildDocument
	{
		//NOTE: this is keyed on the filepath of resolved imports and original expression of unresolved imports
		//the reason for this is that a single expression can resolve to multiple imports
		public List<Import> Imports { get; } = new List<Import> ();

		/// <summary>
		/// Things that derive data from the imports can use this to avoid unnecessarily rebuilding it
		/// </summary>
		public int ImportsHash { get; private set; }

		public AnnotationTable<XObject> Annotations { get; } = new AnnotationTable<XObject> ();
		public List<MSBuildDiagnostic> Diagnostics { get; }
		public bool IsToplevel { get; }

		public MSBuildProjectElement ProjectElement { get; private set; }

		public MSBuildDocument (string filename, bool isToplevel)
		{
			Filename = filename;
			IsToplevel = isToplevel;

			if (isToplevel) {
				Diagnostics = new List<MSBuildDiagnostic> ();
			}
		}

		public string Filename { get; }
		public MSBuildSchema Schema { get; internal set; }
		public MSBuildInferredSchema InferredSchema { get; private set; }

		public void Build (XDocument doc, MSBuildParserContext context)
		{
			var project = doc.Nodes.OfType<XElement> ().FirstOrDefault (x => x.NameEquals ("Project", true));
			if (project == null) {
				//TODO: error
				return;
			}

			var projectElement = new MSBuildProjectElement (project);
			if (IsToplevel) {
				ProjectElement = projectElement;
			}

			ResolveImports (projectElement, context);

			InferredSchema = MSBuildInferredSchema.Build (projectElement, Filename, IsToplevel, context);
		}

		static void GetPropertiesToTrack (PropertyValueCollector propertyVals, MSBuildProjectElement project)
		{
			foreach (var el in project.Elements) {
				if (el is MSBuildImportElement imp) {
					var impAtt = imp.ProjectAttribute?.Value;
					if (impAtt != null) {
						MarkProperties (impAtt);
					}
				} else if (el is MSBuildUsingTaskElement ut) {
					var afAtt = ut.AssemblyFileAttribute?.Value;
					if (afAtt != null) {
						MarkProperties (afAtt);
					}
				}
			}

			void MarkProperties (ExpressionNode expr)
			{
				foreach (var prop in expr.WithAllDescendants ().OfType<ExpressionProperty> ()) {
					propertyVals.Mark (prop.Name);
				}
			}
		}

		void ResolveImports (MSBuildProjectElement project, MSBuildParserContext context)
		{
			var sdks = ResolveSdks (project, context).ToList ();

			//tag the properties we need to track for the imports
			GetPropertiesToTrack (context.PropertyCollector, project);

			var importResolver = context.CreateImportResolver (Filename);

			AddSdkImports ("Sdk.props", sdks, context.PropertyCollector, importResolver);

			void ExtractProperties (MSBuildPropertyGroupElement pg)
			{
				foreach (var prop in pg.Elements) {
					context.PropertyCollector.Collect (importResolver.FileEvaluationContext, prop.ElementName, prop.Value);
				}
			}

			foreach (var el in project.Elements) {
				switch (el) {
				case MSBuildPropertyGroupElement pg:
					ExtractProperties (pg);
					break;
				case MSBuildChooseElement choose:
					foreach (var c in choose.Elements) {
						foreach (var pg in c.GetElements<MSBuildPropertyGroupElement> ()) {
							ExtractProperties (pg);
						}
					}
					break;
				case MSBuildImportElement imp:
					ResolveImport (imp, context, importResolver);
					break;
				}
			}

			AddSdkImports("Sdk.targets", sdks, context.PropertyCollector, importResolver);
		}

		void ResolveImport (MSBuildImportElement element, MSBuildParserContext parseContext, MSBuildImportResolver importResolver)
		{
			var importAtt = element.ProjectAttribute;

			ExpressionNode importPath = null;
			string importTxt = null;

			if (importAtt?.Value != null) {
				importPath = importAtt.Value;
				importTxt = importAtt.XAttribute.Value;
			}

			SdkInfo sdkInfo = null;
			string sdkString = null;

			if (element.SdkAttribute is MSBuildAttribute sdkAtt && sdkAtt.Value is ExpressionText sdkTxt) {
				var loc = sdkAtt.XAttribute.ValueSpan;

				sdkString = sdkTxt.Value;

				if (string.IsNullOrEmpty (sdkString)) {
					if (IsToplevel) {
						Diagnostics.Add (CoreDiagnostics.EmptySdkAttribute, sdkAtt.XAttribute.Span);
					}
					return;
				}

				sdkInfo = parseContext.ResolveSdk (this, sdkString, loc);

				if (sdkInfo == null) {
					if (IsToplevel) {
						Diagnostics.Add (CoreDiagnostics.UnresolvedSdk, loc, sdkTxt.Value);
					}
					return;
				}

				if (string.Equals(sdkInfo.Name, MSBuildCompletionExtensions.WorkloadAutoImportPropsLocatorName, System.StringComparison.OrdinalIgnoreCase)) {
					if (sdkInfo.Paths.Count == 0) {
						return;
					}
				}

				foreach (var p in sdkInfo.Paths) {
					Annotations.Add (sdkAtt.XAttribute, new NavigationAnnotation (p, loc));
				}
			}

			if (importPath != null) {
				var loc = importAtt.XAttribute.ValueSpan;

				foreach (var import in importResolver.Resolve (importPath, importTxt, sdkString, sdkInfo)) {
					AddImport (import);

					if (IsToplevel) {
						if (import.IsResolved) {
							Annotations.Add (importAtt.XAttribute, new NavigationAnnotation (import.Filename, loc));
						} else {
							ReportUnresolvedImport (import, loc, element.ConditionAttribute is not null);
						}
					}
				}
			}
		}

		void ReportUnresolvedImport (Import import, TextSpan location, bool isConditioned)
		{
			if (import.Sdk is not null) {
				Diagnostics.Add (
					isConditioned ? CoreDiagnostics.UnresolvedSdkImportConditioned : CoreDiagnostics.UnresolvedSdkImport,
					location,
					import.OriginalImport,
					import.Sdk);
			} else {
				Diagnostics.Add (
					isConditioned ? CoreDiagnostics.UnresolvedImportConditioned : CoreDiagnostics.UnresolvedImport,
					location,
					import.OriginalImport);
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

		IEnumerable<(string sdk, SdkInfo resolved, TextSpan span)> ResolveSdks (MSBuildProjectElement project, MSBuildParserContext context)
		{
			var sdksAtt = project.SdkAttribute?.XAttribute;
			if (sdksAtt == null) {
				yield break;
			}

			string sdks = sdksAtt.Value;
			if (string.IsNullOrEmpty (sdks)) {
				yield break;
			}

			int offset = IsToplevel ? sdksAtt.ValueOffset : sdksAtt.Span.Start;

			foreach (var sdk in SplitSdkValue (offset, sdksAtt.Value)) {
				if (sdk.id == null) {
					if (IsToplevel) {
						Diagnostics.Add (CoreDiagnostics.EmptySdkAttribute, sdk.span);
					}
				}
				else {
					var sdkInfo = context.ResolveSdk (this, sdk.id, sdk.span);
					if (sdkInfo == null) {
						continue;
					}

					yield return (sdk.id, sdkInfo, sdk.span);

					if (IsToplevel) {
						foreach (var sdkPath in sdkInfo.Paths) {
							Annotations.Add (sdksAtt, new NavigationAnnotation (sdkPath, sdk.span) { IsSdk = true });
						}
					}
				}
			}
		}

		public virtual void AddImport (Import import)
		{
			ImportsHash ^= import.Document?.GetHashCode () ?? 0;
			Imports.Add (import);
		}

		public void AddImport (IEnumerable<Import> imports)
		{
			foreach (var import in imports) {
				AddImport (import);
			}
		}

		void AddSdkImports (string import, IEnumerable<(string id, SdkInfo resolved, TextSpan loc)> sdks, PropertyValueCollector propVals, MSBuildImportResolver importResolver)
		{
			var importExpr = new ExpressionText (0, import, true);
			foreach (var sdk in sdks) {
				var sdkTargets = importResolver.Resolve (importExpr, import, sdk.id, sdk.resolved).FirstOrDefault ();
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

		public IEnumerable<MSBuildDocument> GetDescendentDocuments ()
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
		public IEnumerable<IMSBuildSchema> GetSchemas (bool skipThisDocumentInferredSchema = false)
		{
			if (Schema != null) {
				yield return Schema;
			}
			foreach (var d in GetDescendentDocuments ()) {
				if (d?.Schema is IMSBuildSchema s) {
					yield return s;
				}
			}
			if (!skipThisDocumentInferredSchema && InferredSchema is MSBuildInferredSchema inferred) {
				yield return inferred;
			}
			foreach (var d in GetDescendentDocuments ()) {
				if (d.InferredSchema is IMSBuildSchema descendent) {
					yield return descendent;
				}
			}
		}

		/// <summary>
		/// Gets the files in which the given info has been seen, excluding the current one.
		/// </summary>
		public IEnumerable<string> GetFilesSeenIn (ISymbol info)
		{
			var files = new HashSet<string> ();
			foreach (var doc in GetDescendentDocuments ()) {
				if (doc.Filename != null && doc.InferredSchema.ContainsInfo (info)) {
					files.Add (doc.Filename);
				}
			}
			return files;
		}
	}
}