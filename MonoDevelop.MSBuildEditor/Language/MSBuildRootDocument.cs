// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using MonoDevelop.Core;
using MonoDevelop.Core.Text;
using MonoDevelop.Ide.Editor;
using MonoDevelop.MSBuildEditor.Evaluation;
using MonoDevelop.MSBuildEditor.Schema;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Parser;
using NuGet.Frameworks;

namespace MonoDevelop.MSBuildEditor.Language
{
	class MSBuildRootDocument : MSBuildDocument, IEnumerable<IMSBuildSchema>
	{
		MSBuildToolsVersion? toolsVersion;

		public IReadOnlyList<NuGetFramework> Frameworks { get; private set; }
		public IRuntimeInformation RuntimeInformation { get; private set; }
		public ITextSource Text { get; private set; }
		public XDocument XDocument { get; private set; }

		public MSBuildRootDocument (string filename) : base (filename, true)
		{
		}

		public string GetTargetFrameworkNuGetSearchParameter ()
		{
			if (Frameworks.Count == 1) {
				return Frameworks [0].DotNetFrameworkName;
			}
			//TODO properly support multiple filters
			return null;
		}

		public static MSBuildRootDocument Parse (
			string filename, ITextSource textSource, MSBuildRootDocument previous,
			MSBuildSchemaProvider schemaProvider, IRuntimeInformation runtimeInfo,
			CancellationToken token)
		{
			var xmlParser = new XmlParser (new XmlRootState (), true);
			try {
				xmlParser.Parse (textSource.CreateReader ());
			} catch (Exception ex) {
				LoggingService.LogError ("Unhandled error parsing xml document", ex);
			}

			var xdocument = xmlParser.Nodes.GetRoot ();

			if (xdocument != null && xdocument.RootElement != null) {
				if (!xdocument.RootElement.IsEnded)
					xdocument.RootElement.End (xmlParser.Location);
			}

			//FIXME: unfortunately the XML parser's regions only have line+col locations, not offsets
			//so we need to create an ITextDocument to extract tag bodies
			//we should fix this by changing the parser to use offsets for the tag locations
			ITextDocument textDoc = textSource as ITextDocument
				?? TextEditorFactory.CreateNewDocument (textSource, filename, MSBuildTextEditorExtension.MSBuildMimeType);

			var propVals = new PropertyValueCollector (true);

			string projectPath = filename;

			var doc = new MSBuildRootDocument (filename);

			doc.XDocument = xdocument;
			doc.Text = textDoc;
			doc.RuntimeInformation = runtimeInfo;
			doc.Errors.AddRange (xmlParser.Errors);

			var importedFiles = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
			importedFiles.Add (filename);

			var taskBuilder = new TaskMetadataBuilder (doc);

			try {
				doc.Build (
					xdocument, textDoc, runtimeInfo, propVals, taskBuilder,
					(imp, sdk) => doc.ResolveImport (importedFiles, previous, projectPath, filename, imp, sdk, propVals, taskBuilder, schemaProvider, token)
				);
			} catch (Exception ex) {
				LoggingService.LogError ("Error building document", ex);
			}

			try {
				var binpath = doc.RuntimeInformation.GetBinPath ();
				foreach (var t in Directory.GetFiles (binpath, "*.tasks")) {
					doc.LoadTasks (importedFiles, previous, t, propVals, taskBuilder, schemaProvider, token);
				}
				foreach (var t in Directory.GetFiles (binpath, "*.overridetasks")) {
					doc.LoadTasks (importedFiles, previous, t, propVals, taskBuilder, schemaProvider, token);
				}
			} catch (Exception ex) {
				LoggingService.LogError ("Error resolving tasks", ex);
			}


			try {
				if (previous != null) {
					// try to recover some values that may have been collected from the imports, as they
					// will not have been re-evaluated
					var fx = previous.Frameworks.FirstOrDefault ();
					if (fx != null) {
						propVals.Collect ("TargetFramework", fx.GetShortFolderName ());
						propVals.Collect ("TargetFrameworkVersion", FrameworkInfoProvider.FormatDisplayVersion (fx.Version));
						propVals.Collect ("TargetFrameworkIdentifier", fx.Framework);
					}
				}
				doc.Frameworks = propVals.GetFrameworks ();
			} catch (Exception ex) {
				LoggingService.LogError ("Error determining project framework", ex);
				doc.Frameworks = new List<NuGetFramework> ();
			}

			try {
				doc.Schema = previous?.Schema ?? schemaProvider.GetSchema (filename, null);
			} catch (Exception ex) {
				LoggingService.LogError ("Error loading schema", ex);
			}

			try {
				//this has to run in a second pass so that it runs after all the schemas are loaded
				var validator = new MSBuildDocumentValidator ();
				validator.Run (doc.XDocument, filename, textDoc, doc);
			} catch (Exception ex) {
				LoggingService.LogError ("Error in validation", ex);
			}

			return doc;
		}

		void LoadTasks (
			HashSet<string> importedFiles, MSBuildDocument previous, string filename,
			PropertyValueCollector propVals, TaskMetadataBuilder taskBuilder, MSBuildSchemaProvider schemaProvider,
			CancellationToken token)
		{
			try {
				var import = GetCachedOrParse (importedFiles, previous, filename, null, File.GetLastWriteTimeUtc (filename), Filename, propVals, taskBuilder, schemaProvider, token);
				Imports.Add (filename, import);
			} catch (Exception ex) {
				LoggingService.LogError ($"Error loading tasks file {filename}", ex);
			}
		}

		Import ParseImport (
			HashSet<string> importedFiles, Import import, string projectPath,
			PropertyValueCollector propVals, TaskMetadataBuilder taskBuilder, MSBuildSchemaProvider schemaProvider,
			CancellationToken token)
		{
			token.ThrowIfCancellationRequested ();

			var xmlParser = new XmlParser (new XmlRootState (), true);
			ITextDocument textDoc;
			try {
				textDoc = TextEditorFactory.CreateNewDocument (import.Filename, MSBuildTextEditorExtension.MSBuildMimeType);
				xmlParser.Parse (textDoc.CreateReader ());
			} catch (Exception ex) {
				LoggingService.LogError ("Unhandled error parsing xml document", ex);
				return import;
			}

			var doc = xmlParser.Nodes.GetRoot ();

			import.Document = new MSBuildDocument (import.Filename, false);
			import.Document.Build (
				doc, textDoc, RuntimeInformation, propVals, taskBuilder,
				(imp, sdk) => ResolveImport (importedFiles, null, projectPath, import.Filename, imp, sdk, propVals, taskBuilder, schemaProvider, token)
			);

			import.Document.Schema = schemaProvider.GetSchema (import.Filename, import.Sdk);

			return import;
		}

		IEnumerable<Import> ResolveImport (HashSet<string> importedFiles, MSBuildRootDocument oldDoc, string projectPath, string thisFilePath, string importExpr, string sdk, PropertyValueCollector propVals, TaskMetadataBuilder taskBuilder, MSBuildSchemaProvider schemaProvider, CancellationToken token)
		{
			//TODO: re-use these contexts instead of recreating them
			var importEvalCtx = MSBuildEvaluationContext.Create (RuntimeInformation, projectPath, thisFilePath);

			bool foundAny = false;

			//the ToList is necessary because nested parses can alter the list between this yielding values 
			foreach (var filename in importEvalCtx.EvaluatePathWithPermutation (importExpr, Path.GetDirectoryName (thisFilePath), propVals).ToList ()) {
				if (string.IsNullOrEmpty (filename)) {
					continue;
				}

				//wildcards
				var wildcardIdx = filename.IndexOf ('*');
				//arbitrary limit to skip improbably short values from bad evaluation
				const int MIN_WILDCARD_STAR_IDX = 15;
				const int MIN_WILDCARD_PATTERN_IDX = 10;
				if (wildcardIdx > MIN_WILDCARD_STAR_IDX) {
					var lastSlash = filename.LastIndexOf (Path.DirectorySeparatorChar);
					if (lastSlash < MIN_WILDCARD_PATTERN_IDX) {
						continue;
					}
					if (lastSlash > wildcardIdx) {
						continue;
					}

					string [] files;
					try {
						var dir = filename.Substring (0, lastSlash);
						if (!importedFiles.Add (dir)) {
							foundAny = true;
							continue;
						}
						if (!Directory.Exists (dir)) {
							continue;
						}

						//finding the folder's enough for this to "count" as resolved even if there aren't any files in it
						foundAny = true;

						var pattern = filename.Substring (lastSlash + 1);

						files = Directory.GetFiles (dir, pattern);
					} catch (Exception ex) {
						LoggingService.LogError ($"Error evaluating wildcard in import candidate '{filename}'", ex);
						continue;
					}

					foreach (var f in files) {
						Import wildImport;
						try {
							wildImport = GetCachedOrParse (importedFiles, oldDoc, f, sdk, File.GetLastWriteTimeUtc (f), projectPath, propVals, taskBuilder, schemaProvider, token);
						} catch (Exception ex) {
							LoggingService.LogError ($"Error reading wildcard import candidate '{files}'", ex);
							continue;
						}
						yield return wildImport;
					}
					continue;
				}

				if (!importedFiles.Add (filename)) {
					foundAny = true;
					continue;
				}

				Import import;
				try {
					var fi = new FileInfo (filename);
					if (!fi.Exists) {
						continue;
					}
					import = GetCachedOrParse (importedFiles, oldDoc, filename, sdk, fi.LastWriteTimeUtc, projectPath, propVals, taskBuilder, schemaProvider, token);
				} catch (Exception ex) {
					LoggingService.LogError ($"Error reading import candidate '{filename}'", ex);
					continue;
				}

				foundAny = true;
				yield return import;
				continue;
			}

			if (!foundAny) {
				if (oldDoc == null && failedImports.Add (importExpr)) {
					LoggingService.LogDebug ($"Could not resolve MSBuild import '{importExpr}'");
				}
				yield return new Import (importExpr, sdk, DateTime.MinValue);
			}
		}

		Import GetCachedOrParse (
			HashSet<string> importedFiles, MSBuildDocument oldDoc, string filename, string sdk, DateTime mtimeUtc, string projectPath,
			PropertyValueCollector propVals, TaskMetadataBuilder taskBuilder, MSBuildSchemaProvider schemaProvider,
			CancellationToken token)
		{
			if (oldDoc != null && oldDoc.Imports.TryGetValue (filename, out Import oldImport) && oldImport.TimeStampUtc == mtimeUtc) {
				//TODO: check mtimes of descendent imports too
				return oldImport;
			} else {
				//TODO: guard against cyclic imports
				return ParseImport (importedFiles, new Import (filename, sdk, mtimeUtc), projectPath, propVals, taskBuilder, schemaProvider, token);
			}
		}

		public IEnumerator<IMSBuildSchema> GetEnumerator ()
		{
			return GetSchemas ().GetEnumerator ();
		}

		IEnumerator IEnumerable.GetEnumerator ()
		{
			return GetSchemas ().GetEnumerator ();
		}

		static readonly HashSet<string> failedImports = new HashSet<string> ();

		public MSBuildToolsVersion ToolsVersion {
			get {
				if (toolsVersion.HasValue) {
					return toolsVersion.Value;
				}

				if (XDocument.RootElement != null) {
					var sdkAtt = XDocument.RootElement.Attributes [new XName ("Sdk")];
					if (sdkAtt != null) {
						toolsVersion = MSBuildToolsVersion.V15_0;
						return toolsVersion.Value;
					}

					var tvAtt = XDocument.RootElement.Attributes [new XName ("ToolsVersion")];
					if (tvAtt != null) {
						var val = tvAtt.Value;
						MSBuildToolsVersion tv;
						if (MSBuildToolsVersionExtensions.TryParse (val, out tv)) {
							toolsVersion = tv;
							return tv;
						}
					}
				}

				toolsVersion = MSBuildToolsVersion.Unknown;
				return toolsVersion.Value;
			}
		}
	}
}
