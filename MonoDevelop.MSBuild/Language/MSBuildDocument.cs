// Copyright (c) 2014 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP
#nullable enable
#endif

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

using MonoDevelop.MSBuild.Analysis;
using MonoDevelop.MSBuild.Dom;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Language.Syntax;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.MSBuild.SdkResolution;
using MonoDevelop.MSBuild.Workspace;
using MonoDevelop.Xml.Dom;

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

		public AnnotationTable<XObject>? Annotations { get; }
		public List<MSBuildDiagnostic>? Diagnostics { get; }

		[MemberNotNullWhen(true, nameof(Diagnostics), nameof(Annotations))]
		public bool IsTopLevel => Diagnostics is not null;

		public MSBuildFileKind FileKind { get; }

		public MSBuildProjectElement? ProjectElement { get; private set; }

		public MSBuildDocument (string? filename, bool isTopLevel)
		{
			Filename = filename;
			FileKind = MSBuildFileKindExtensions.GetFileKind (Filename);

			if (isTopLevel) {
				Diagnostics = new ();
				Annotations = new ();
			}
		}

		/// <summary>
		/// The filename. May be null when editing an unsaved file.
		/// </summary>
		public string? Filename { get; }

		public MSBuildSchema? Schema { get; internal set; }
		public MSBuildInferredSchema? InferredSchema { get; private set; }

		public void Build (XDocument doc, MSBuildParserContext context)
		{
			var project = doc.Nodes.OfType<XElement> ().FirstOrDefault (x => x.Name.Equals (MSBuildElementName.Project, true));
			if (project is null) {
				//TODO: error
				return;
			}

			var projectElement = new MSBuildProjectElement (project);
			if (IsTopLevel) {
				ProjectElement = projectElement;
			}

			ResolveImports (projectElement, context);

			InferredSchema = MSBuildInferredSchema.Build (projectElement, Filename, IsTopLevel, context);
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
			var projectAttributeSdks = GetProjectAttributeSdks (project, context).ToList ();

			var sdkElements = project.SdkElements;

			//tag the properties we need to track for the imports
			GetPropertiesToTrack (context.PropertyCollector, project);

			var importResolver = context.CreateImportResolver (Filename);

			var sdkPropsExpr = new ExpressionText (0, "Sdk.props", true);
			var sdkTargetsExpr = new ExpressionText (0, "Sdk.targets", true);

			void AddSdkImport (ExpressionText importExpr, string importText, string sdkString, SdkInfo sdk, bool isImplicit = false)
			{
				foreach (var sdkImport in importResolver.Resolve (importExpr, importText, sdkString, sdk, isImplicit)) {
					AddImport (sdkImport);
				}
			}

			foreach (var sdkElement in sdkElements) {
				if (sdkElement.NameAttribute is not null) {
					var resolvedSdk = importResolver.ResolveSdk (this, sdkElement);
					if (resolvedSdk is not null) {
						// technically we should re-resolve Sdk elements before doing the Sdk.targets import
						// as it may use properties that change between the Sdk.props and Sdk.targets
						// but that seems broken and wasteful so ignore it for now
						projectAttributeSdks.Add (resolvedSdk);
					}
				}
			}

			foreach (var sdk in projectAttributeSdks) {
				AddSdkImport (sdkPropsExpr, sdkPropsExpr.Value, sdk.ToString(), sdk, false);
			}

			void ExtractProperties (MSBuildPropertyGroupElement pg)
			{
				foreach (var prop in pg.Elements) {
					context.PropertyCollector.Collect (importResolver.FileEvaluationContext, prop.Name, prop.Value);
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
				case MSBuildImportGroupElement importGroup:
					foreach (var import in importGroup.GetElements<MSBuildImportElement> ()) {
						ResolveImport (import, context, importResolver);
					}
					break;
				}
			}

			foreach (var sdk in projectAttributeSdks) {
				AddSdkImport (sdkTargetsExpr, sdkTargetsExpr.Value, sdk.ToString (), sdk, false);
			}
		}

		void ResolveImport (MSBuildImportElement element, MSBuildParserContext parseContext, MSBuildImportResolver importResolver)
		{
			var importAtt = element.ProjectAttribute;

			SdkInfo? sdk = null;
			if (element.SdkAttribute is not null) {
				sdk = importResolver.ResolveSdk (this, element);
				if (sdk is null) {
					// TODO: add placeholder import
					return;
				}
				if (sdk is not null && string.Equals (sdk.Name, MSBuildCompletionExtensions.WorkloadAutoImportPropsLocatorName, System.StringComparison.OrdinalIgnoreCase)) {
					if (sdk.Paths.Count == 0) {
						return;
					}
				}
			}

			if (importAtt is not null && importAtt.Value is ExpressionNode importPath && importAtt.XAttribute.HasValue) {
				var loc = importAtt.XAttribute.ValueSpan.Value;

				foreach (var import in importResolver.Resolve (importPath, importAtt.XAttribute.Value, sdk?.ToString(), sdk)) {
					AddImport (import);

					if (IsTopLevel) {
						if (import.IsResolved) {
							Annotations.Add (importAtt.XAttribute, new NavigationAnnotation (import.Filename, loc));
						} else {
							ReportUnresolvedImport (Diagnostics, import, loc, element.ConditionAttribute is not null);
						}
					}
				}
			}
		}

		static void ReportUnresolvedImport (List<MSBuildDiagnostic> diagnostics, Import import, TextSpan location, bool isConditioned)
		{
			if (import.Sdk is not null) {
				diagnostics.Add (
					isConditioned ? CoreDiagnostics.UnresolvedSdkImportConditioned : CoreDiagnostics.UnresolvedSdkImport,
					location,
					import.OriginalImport,
					import.Sdk);
			} else {
				diagnostics.Add (
					isConditioned ? CoreDiagnostics.UnresolvedImportConditioned : CoreDiagnostics.UnresolvedImport,
					location,
					import.OriginalImport);
			}
		}

		static IEnumerable<(string? id, TextSpan span)> SplitSdkValue (int offset, string value)
		{
			int start = 0, end;
			while ((end = value.IndexOf (';', start)) > -1) {
				yield return MakeResult ();
				start = end + 1;
			}
			end = value.Length;
			yield return MakeResult ();

			TextSpan CreateSpan (int s, int e) => TextSpan.FromBounds (offset + s, offset + e);

			(string? id, TextSpan loc) MakeResult ()
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

		IEnumerable<SdkInfo> GetProjectAttributeSdks (MSBuildProjectElement project, MSBuildParserContext context)
		{
			var sdksAtt = project.SdkAttribute?.XAttribute;
			if (sdksAtt == null) {
				yield break;
			}

			string? sdks = sdksAtt.Value;
			if (string.IsNullOrEmpty (sdks)) {
				if (IsTopLevel) {
					Diagnostics.Add (CoreDiagnostics.EmptySdkName, sdksAtt.ValueSpan ?? sdksAtt.NameSpan);
				}
				yield break;
			}

			int offset = IsTopLevel && sdksAtt.HasValue ? sdksAtt.ValueOffset.Value : sdksAtt.Span.Start;

			foreach (var sdk in SplitSdkValue (offset, sdksAtt.Value)) {
				if (string.IsNullOrEmpty (sdk.id)) {
					if (IsTopLevel) {
						Diagnostics.Add (CoreDiagnostics.EmptySdkName, sdk.span);
					}
				} else {
					if (!context.TryParseSdkReferenceFromProjectSdk (this, sdk.id, sdk.span, out var parsedReference)) {
						continue;
					}
					var sdkInfo = context.ResolveSdk (this, parsedReference, sdk.span);
					if (sdkInfo is null) {
						continue;
					}
					if (IsTopLevel) {
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
		public virtual IEnumerable<IMSBuildSchema> GetSchemas (bool skipThisDocumentInferredSchema = false)
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
		public IEnumerable<string> GetDescendedDocumentsReferencingSymbol (ISymbol info)
		{
			var files = new HashSet<string> ();
			foreach (var doc in GetDescendentDocuments ()) {
				if (doc.Filename != null && doc.InferredSchema is { } schema && schema.ContainsInfo (info)) {
					files.Add (doc.Filename);
				}
			}
			return files;
		}
	}
}