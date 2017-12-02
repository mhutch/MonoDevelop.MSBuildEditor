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

namespace MonoDevelop.MSBuildEditor.Language
{
	class MSBuildRootDocument : MSBuildDocument, IEnumerable<IMSBuildSchema>
	{
		MSBuildToolsVersion? toolsVersion;

		public IReadOnlyList<FrameworkReference> Frameworks { get; private set; }
		public IRuntimeInformation RuntimeInformation { get; private set; }
		public ITextSource Text { get; private set; }
		public XDocument XDocument { get; private set; }

		public MSBuildRootDocument (string filename) : base (filename, true)
		{
		}

		public string GetTargetFramework ()
		{
			return Frameworks.FirstOrDefault ()?.ToString () ?? ".NETStandard,Version=v2.0";
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

			doc.Build (
				xdocument, textDoc, runtimeInfo, propVals,
				(imp, sdk, props) => doc.ResolveImport (previous, projectPath, filename, imp, sdk, props, schemaProvider, token)
			);

			var binpath = doc.RuntimeInformation.GetBinPath ();
			foreach (var t in Directory.GetFiles (binpath, "*.tasks")) {
				doc.LoadTasks (previous, t, propVals, schemaProvider, token);
			}
			foreach (var t in Directory.GetFiles (binpath, "*.overridetasks")) {
				doc.LoadTasks (previous, t, propVals, schemaProvider, token);
			}

			doc.Errors.AddRange (xmlParser.Errors);

			if (previous != null) {
				doc.Schema = previous.Schema;
				// try to recover some values that may have been collected from the imports, as they
				// will not have been re-evaluated
				var fx = previous.Frameworks.FirstOrDefault ();
				if (fx != null) {
					propVals.Collect ("TargetFramework", fx.ShortName);
					propVals.Collect ("TargetFrameworkVersion", fx.Version);
					propVals.Collect ("TargetFrameworkIdentifier", fx.Identifier);
				}
			} else {
				doc.Schema = schemaProvider.GetSchema (filename, null);
			}

			//this has to run in a second pass so that it runs after all the schemas are loaded
			var validator = new MSBuildDocumentValidator ();
			validator.Run (doc.XDocument, filename, textDoc, doc);

			doc.Frameworks = propVals.GetFrameworks ();

			return doc;
		}

		void LoadTasks (MSBuildDocument previous, string filename, PropertyValueCollector propVals, MSBuildSchemaProvider schemaProvider, CancellationToken token)
		{
			try {
				var import = GetCachedOrParse (previous, filename, null, File.GetLastWriteTimeUtc (filename), Filename, propVals, schemaProvider, token);
				Imports.Add (filename, import);
			} catch (Exception ex) {
				LoggingService.LogError ($"Error loading tasks file {filename}", ex);
			}
		}

		Import ParseImport (Import import, string projectPath, PropertyValueCollector propVals, MSBuildSchemaProvider schemaProvider, CancellationToken token)
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

			import.Document = new MSBuildDocument (Filename, false);
			import.Document.Build (
				doc, textDoc, RuntimeInformation, propVals,
				(imp, sdk, props) => ResolveImport (null, projectPath, import.Filename, imp, sdk, props, schemaProvider, token)
			);

			import.Document.Schema = schemaProvider.GetSchema (import.Filename, import.Sdk);

			return import;
		}

		IEnumerable<Import> ResolveImport (MSBuildRootDocument oldDoc, string projectPath, string thisFilePath, string importExpr, string sdk, PropertyValueCollector propVals, MSBuildSchemaProvider schemaProvider, CancellationToken token)
		{
			//TODO: re-use these contexts instead of recreating them
			var importEvalCtx = MSBuildEvaluationContext.Create (
				ToolsVersion, RuntimeInformation, projectPath, thisFilePath
			);

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
							wildImport = GetCachedOrParse (oldDoc, f, sdk, File.GetLastWriteTimeUtc (f), projectPath, propVals, schemaProvider, token);
						} catch (Exception ex) {
							LoggingService.LogError ($"Error reading wildcard import candidate '{files}'", ex);
							continue;
						}
						yield return wildImport;
					}
					continue;
				}

				Import import;
				try {
					var fi = new FileInfo (filename);
					if (!fi.Exists) {
						continue;
					}
					import = GetCachedOrParse (oldDoc, filename, sdk, fi.LastWriteTimeUtc, projectPath, propVals, schemaProvider, token);
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

		Import GetCachedOrParse (MSBuildDocument oldDoc, string filename, string sdk, DateTime mtimeUtc, string projectPath, PropertyValueCollector propVals, MSBuildSchemaProvider schemaProvider, CancellationToken token)
		{
			if (oldDoc != null && oldDoc.Imports.TryGetValue (filename, out Import oldImport) && oldImport.TimeStampUtc == mtimeUtc) {
				//TODO: check mtimes of descendent imports too
				return oldImport;
			} else {
				//TODO: guard against cyclic imports
				return ParseImport (new Import (filename, sdk, mtimeUtc), projectPath, propVals, schemaProvider, token);
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
