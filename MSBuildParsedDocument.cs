// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using MonoDevelop.Core;
using MonoDevelop.Core.Assemblies;
using MonoDevelop.Core.Text;
using MonoDevelop.Ide.Editor;
using MonoDevelop.Ide.TypeSystem;
using MonoDevelop.MSBuildEditor.Evaluation;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Editor;
using MonoDevelop.Xml.Parser;
using MonoDevelop.MSBuildEditor.Schema;
using MonoDevelop.MSBuildEditor.Language;

namespace MonoDevelop.MSBuildEditor
{
	class MSBuildParsedDocument : XmlParsedDocument
	{
		MSBuildToolsVersion? toolsVersion;

		public MSBuildResolveContext Context { get; internal set; }

		public MSBuildParsedDocument (string filename) : base (filename)
		{
		}

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
						if (Enum.TryParse (val, out tv)) {
							toolsVersion = tv;
							return tv;
						}
					}
				}

				toolsVersion = MSBuildToolsVersion.Unknown;
				return toolsVersion.Value;
			}
		}

		public IReadOnlyList<TargetFrameworkMoniker> Frameworks { get; private set; }

		public MSBuildSdkResolver SdkResolver { get; private set; }
		public MSBuildRuntimeInformation RuntimeInformation { get; private set; }
		public ITextSource Text { get; private set; }

		internal static ParsedDocument ParseInternal (ParseOptions options, CancellationToken token)
		{
			var doc = new MSBuildParsedDocument (options.FileName);
			doc.Flags |= ParsedDocumentFlags.NonSerializable;

			var xmlParser = new XmlParser (new XmlRootState (), true);
			try {
				xmlParser.Parse (options.Content.CreateReader ());
			} catch (Exception ex) {
				LoggingService.LogError ("Unhandled error parsing xml document", ex);
			}

			doc.XDocument = xmlParser.Nodes.GetRoot ();

			doc.Text = options.Content;

			doc.AddRange (xmlParser.Errors);

			if (doc.XDocument != null && doc.XDocument.RootElement != null) {
				if (!doc.XDocument.RootElement.IsEnded)
					doc.XDocument.RootElement.End (xmlParser.Location);
			}

			var oldDoc = (MSBuildParsedDocument)options.OldParsedDocument;

			//FIXME: unfortunately the XML parser's regions only have line+col locations, not offsets
			//so we need to create an ITextDocument to extract tag bodies
			//we should fix this by changing the parser to use offsets for the tag locations
			var textDoc = TextEditorFactory.CreateNewDocument (options.Content, options.FileName, MSBuildTextEditorExtension.MSBuildMimeType);

			var runtime = Runtime.SystemAssemblyService.CurrentRuntime;
			doc.SdkResolver = oldDoc?.SdkResolver ?? new MSBuildSdkResolver (runtime);
			doc.RuntimeInformation = oldDoc?.RuntimeInformation ?? new MSBuildRuntimeInformation (runtime, MSBuildToolsVersion.Unknown, doc.SdkResolver);

			var propVals = new PropertyValueCollector (true);

			var schemaProvider = new MonoDevelopMSBuildSchemaProvider ();

			string projectPath = options.FileName;
			doc.Context = MSBuildResolveContext.Create (
				options.FileName,
				true,
				doc.XDocument,
				textDoc,
				doc.SdkResolver,
				propVals,
				(ctx, imp, sdk, props) => doc.ResolveImport (oldDoc, projectPath, options.FileName, imp, sdk, props, schemaProvider, token)
			);

			doc.AddRange (doc.Context.Errors);

			doc.Frameworks = propVals.GetFrameworks ();

			return doc;
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

			import.ResolveContext = MSBuildResolveContext.Create (
				import.Filename,
				false,
				doc,
				textDoc,
				SdkResolver,
				propVals,
				(ctx, imp, sdk, props) => ResolveImport (null, projectPath, import.Filename, imp, sdk, props, schemaProvider, token)
			);

			import.ResolveContext.Schema = schemaProvider.GetSchema (import);

			return import;
		}

		IEnumerable<Import> ResolveImport (MSBuildParsedDocument oldDoc, string projectPath, string thisFilePath, string importExpr, string sdk, PropertyValueCollector propVals, MSBuildSchemaProvider schemaProvider, CancellationToken token)
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
							wildImport = GetCachedOrParse (f, File.GetLastWriteTimeUtc (f));
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
					import = GetCachedOrParse (filename, fi.LastWriteTimeUtc);
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

			Import GetCachedOrParse (string filename, DateTime mtimeUtc)
			{
				if (oldDoc != null && oldDoc.Context.Imports.TryGetValue (filename, out Import oldImport) && oldImport.TimeStampUtc == mtimeUtc) {
					//TODO: check mtimes of descendent imports too
					return oldImport;
				} else {
					//TODO: guard against cyclic imports
					return ParseImport (new Import (filename, sdk, mtimeUtc), projectPath, propVals, schemaProvider, token);
				}
			}
		}

		static readonly HashSet<string> failedImports = new HashSet<string> ();
	}
}
