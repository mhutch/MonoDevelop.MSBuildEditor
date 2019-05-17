// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Parser;
using NuGet.Frameworks;

namespace MonoDevelop.MSBuild.Language
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
					xdocument.RootElement.End (xmlParser.Position);
			}

			var propVals = new PropertyValueCollector (true);

			string projectPath = filename;

			var doc = new MSBuildRootDocument (filename) {
				XDocument = xdocument,
				Text = textSource,
				RuntimeInformation = runtimeInfo
			};
			doc.Errors.AddRange (xmlParser.Diagnostics);

			try {
				doc.Schema = previous?.Schema ?? schemaProvider.GetSchema (filename, null);
			} catch (Exception ex) {
				LoggingService.LogError ("Error loading schema", ex);
			}

			var importedFiles = new HashSet<string> (StringComparer.OrdinalIgnoreCase) {
				filename
			};

			var taskBuilder = MSBuildHost.CreateTaskMetadataBuilder (doc);

			var extension = Path.GetExtension (filename);

			string MakeRelativeMSBuildPathAbsolute (string path)
			{
				var dir = Path.GetDirectoryName (doc.Filename);
				path = path.Replace ('\\', Path.DirectorySeparatorChar);
				return Path.GetFullPath (Path.Combine (dir, path));
			}

			Import TryImportFile (string label, string possibleFile)
			{
				try {
					var fi = new FileInfo (possibleFile);
					if (fi.Exists) {
						var imp = doc.GetCachedOrParse (importedFiles, previous, label, possibleFile, null, fi.LastWriteTimeUtc, projectPath, propVals, taskBuilder, schemaProvider, token);
						doc.AddImport (imp);
						return imp;
					}
				} catch (Exception ex) {
					LoggingService.LogError ($"Error importing '{possibleFile}'", ex);
				}
				return null;
			}

			Import TryImportSibling (string ifHasThisExtension, string thenTryThisExtension)
			{
				if (string.Equals (ifHasThisExtension, extension, StringComparison.OrdinalIgnoreCase)) {
					var siblingFilename = Path.ChangeExtension (filename, thenTryThisExtension);
					return TryImportFile ("(implicit)", siblingFilename);
				}
				return null;
			}

			void TryImportIntellisenseImports (MSBuildSchema schema)
			{
				foreach (var intellisenseImport in schema.IntelliSenseImports) {
					TryImportFile ("(from schema)", MakeRelativeMSBuildPathAbsolute (intellisenseImport));
				}
			}

			try {
				//if this is a targets file, try to import the props _at the top_
				var propsImport = TryImportSibling (".targets", ".props");

				// this currently only happens in the root file
				// it's a quick hack to allow files to get some basic intellisense by
				// importing the files _that they themselves expect to be imported from_.
				// we also try to load them from the sibling props, as a paired targets/props
				// will likely share a schema file.
				var schema = doc.Schema ?? propsImport?.Document?.Schema;
				if (schema != null) {
					TryImportIntellisenseImports (doc.Schema);
				}

				doc.Build (
					xdocument, textSource, runtimeInfo, propVals, taskBuilder,
					(imp, sdk) => doc.ResolveImport (importedFiles, previous, projectPath, filename, imp, sdk, propVals, taskBuilder, schemaProvider, token)
				);

				//if this is a props file, try to import the targets _at the bottom_
				var targetsImport = TryImportSibling (".props", ".targets");

				//and if we didn't load intellisense import already, try to load them from the sibling targets
				if (schema == null && targetsImport?.Document?.Schema != null) {
					TryImportIntellisenseImports (targetsImport.Document.Schema);
				}
			} catch (Exception ex) {
				LoggingService.LogError ($"Error building document '{projectPath}'", ex);
			}

			try {
				var binpath = doc.RuntimeInformation.GetBinPath ();
				foreach (var t in Directory.GetFiles (binpath, "*.tasks")) {
					doc.LoadTasks (importedFiles, previous, "(core tasks)", t, propVals, taskBuilder, schemaProvider, token);
				}
				foreach (var t in Directory.GetFiles (binpath, "*.overridetasks")) {
					doc.LoadTasks (importedFiles, previous, "(core overridetasks)", t, propVals, taskBuilder, schemaProvider, token);
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
				//this has to run in a second pass so that it runs after all the schemas are loaded
				var validator = new MSBuildDocumentValidator ();
				validator.Run (doc.XDocument, filename, textSource, doc);
			} catch (Exception ex) {
				LoggingService.LogError ("Error in validation", ex);
			}

			return doc;
		}

		void LoadTasks (
			HashSet<string> importedFiles, MSBuildRootDocument previous, string label, string filename,
			PropertyValueCollector propVals, TaskMetadataBuilder taskBuilder, MSBuildSchemaProvider schemaProvider,
			CancellationToken token)
		{
			try {
				var import = GetCachedOrParse (importedFiles, previous, label, filename, null, File.GetLastWriteTimeUtc (filename), Filename, propVals, taskBuilder, schemaProvider, token);
				AddImport (import);
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
			ITextSource textSource;
			try {
				textSource = TextEditorFactory.CreateNewDocument (import.Filename);
				xmlParser.Parse (textSource.CreateReader ());
			} catch (Exception ex) {
				LoggingService.LogError ("Unhandled error parsing xml document", ex);
				return import;
			}

			var doc = xmlParser.Nodes.GetRoot ();

			import.Document = new MSBuildDocument (import.Filename, false);
			import.Document.Build (
				doc, textSource, RuntimeInformation, propVals, taskBuilder,
				(imp, sdk) => ResolveImport (importedFiles, null, projectPath, import.Filename, imp, sdk, propVals, taskBuilder, schemaProvider, token)
			);

			import.Document.Schema = schemaProvider.GetSchema (import.Filename, import.Sdk);

			return import;
		}

		IEnumerable<Import> ResolveImport (HashSet<string> importedFiles, MSBuildRootDocument oldDoc, string projectPath, string thisFilePath, string importExpr, string sdk, PropertyValueCollector propVals, TaskMetadataBuilder taskBuilder, MSBuildSchemaProvider schemaProvider, CancellationToken token)
		{
			//FIXME: add support for MSBuildUserExtensionsPath, the context does not currently support it
			if (importExpr.IndexOf("$(MSBuildUserExtensionsPath)",StringComparison.OrdinalIgnoreCase) > -1) {
				yield break;
			}

			//TODO: re-use these contexts instead of recreating them
			var importEvalCtx = MSBuildHost.CreateEvaluationContext (RuntimeInformation, projectPath, thisFilePath);

			bool foundAny = false;
			bool isWildcard = false;

			//the ToList is necessary because nested parses can alter the list between this yielding values 
			foreach (var filename in importEvalCtx.EvaluatePathWithPermutation (importExpr, Path.GetDirectoryName (thisFilePath), propVals).ToList ()) {
				if (string.IsNullOrEmpty (filename)) {
					continue;
				}

				//dedup
				if (!importedFiles.Add (filename)) {
					foundAny = true;
					continue;
				}

				//wildcards
				var wildcardIdx = filename.IndexOf ('*');

				//arbitrary limit to skip improbably short values from bad evaluation
				const int MIN_WILDCARD_STAR_IDX = 15;
				const int MIN_WILDCARD_PATTERN_IDX = 10;
				if (wildcardIdx > MIN_WILDCARD_STAR_IDX) {
					isWildcard = true;
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
							wildImport = GetCachedOrParse (importedFiles, oldDoc, importExpr, f, sdk, File.GetLastWriteTimeUtc (f), projectPath, propVals, taskBuilder, schemaProvider, token);
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
					import = GetCachedOrParse (importedFiles, oldDoc, importExpr, filename, sdk, fi.LastWriteTimeUtc, projectPath, propVals, taskBuilder, schemaProvider, token);
				} catch (Exception ex) {
					LoggingService.LogError ($"Error reading import candidate '{filename}'", ex);
					continue;
				}

				foundAny = true;
				yield return import;
				continue;
			}

			//yield a placeholder for tooltips, imports pad etc to query
			if (!foundAny) {
				yield return new Import (importExpr, sdk, null, DateTime.MinValue);
			}

			// we skip logging for wildcards as these are generally extensibility points that are often unused
			// this is here (rather than being folded into the next condition) for ease of breakpointing
			if (!foundAny && !isWildcard) {
				if (oldDoc == null && failedImports.Add (importExpr)) {
					LoggingService.LogDebug ($"Could not resolve MSBuild import '{importExpr}'");
				}
			}
		}

		Import GetCachedOrParse (
			HashSet<string> importedFiles, MSBuildRootDocument oldDoc, string importExpr, string resolvedFilename, string sdk, DateTime mtimeUtc, string projectPath,
			PropertyValueCollector propVals, TaskMetadataBuilder taskBuilder, MSBuildSchemaProvider schemaProvider,
			CancellationToken token)
		{
			if (oldDoc != null && oldDoc.resolvedImportsMap.TryGetValue (resolvedFilename ?? importExpr, out Import oldImport) && oldImport.TimeStampUtc == mtimeUtc) {
				//TODO: check mtimes of descendent imports too
				return oldImport;
			} else {
				//TODO: guard against cyclic imports
				return ParseImport (importedFiles, new Import (importExpr, sdk, resolvedFilename, mtimeUtc), projectPath, propVals, taskBuilder, schemaProvider, token);
			}
		}

		readonly Dictionary<string, Import> resolvedImportsMap = new Dictionary<string, Import> ();

		public override void AddImport (Import import)
		{
			base.AddImport (import);
			if (import.IsResolved) {
				resolvedImportsMap [import.Filename] = import;
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
