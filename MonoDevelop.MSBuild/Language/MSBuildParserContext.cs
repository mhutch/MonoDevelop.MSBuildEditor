// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Build.Framework;
using MonoDevelop.MSBuild.Evaluation;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.MSBuild.Util;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Parser;

namespace MonoDevelop.MSBuild.Language
{
	// represents the context for a parse operation
	class MSBuildParserContext
	{
		public MSBuildRootDocument RootDocument { get; }
		public MSBuildRootDocument PreviousRootDocument { get; }
		public HashSet<string> ImportedFiles { get; }
		public string ProjectPath { get; }
		public PropertyValueCollector PropertyCollector { get; }
		public ITaskMetadataBuilder TaskBuilder { get; }
		public MSBuildSchemaProvider SchemaProvider { get; }
		public CancellationToken Token { get; }
		public MSBuildRuntimeEvaluationContext RuntimeEvaluationContext { get; }
		public IRuntimeInformation RuntimeInformation { get; }

		public MSBuildParserContext (
			IRuntimeInformation runtimeInformation,
			MSBuildRootDocument doc,
			MSBuildRootDocument previous,
			HashSet<string> importedFiles,
			string projectPath,
			PropertyValueCollector propVals,
			ITaskMetadataBuilder taskBuilder,
			MSBuildSchemaProvider schemaProvider,
			CancellationToken token)
		{
			RuntimeInformation = runtimeInformation;
			RootDocument = doc;
			PreviousRootDocument = previous;
			ImportedFiles = importedFiles;
			ProjectPath = projectPath;
			PropertyCollector = propVals;
			TaskBuilder = taskBuilder;
			SchemaProvider = schemaProvider;
			Token = token;

			RuntimeEvaluationContext = new MSBuildRuntimeEvaluationContext (runtimeInformation);
		}

		public Import ParseImport (Import import)
		{
			Token.ThrowIfCancellationRequested ();

			var xmlParser = new XmlParser (new XmlRootState (), true);
			ITextSource textSource;
			try {
				textSource = TextSourceFactory.CreateNewDocument (import.Filename);
				xmlParser.Parse (textSource.CreateReader ());
			} catch (Exception ex) {
				LoggingService.LogError ("Unhandled error parsing xml document", ex);
				return import;
			}

			var doc = xmlParser.Nodes.GetRoot ();

			import.Document = new MSBuildDocument (import.Filename, false);
			import.Document.Build (doc, textSource, this);
			import.Document.Schema = SchemaProvider.GetSchema (import.Filename, import.Sdk);

			return import;
		}

		public Import GetCachedOrParse (string importExpr, string resolvedFilename, string sdk, DateTime mtimeUtc)
		{
			var oldDoc = PreviousRootDocument;
			if (oldDoc != null && oldDoc.resolvedImportsMap.TryGetValue (resolvedFilename ?? importExpr, out Import oldImport) && oldImport.TimeStampUtc == mtimeUtc) {
				//TODO: check mtimes of descendent imports too
				return oldImport;
			}
			//TODO: guard against cyclic imports
			return ParseImport (new Import (importExpr, sdk, resolvedFilename, mtimeUtc));
		}

		internal IEnumerable<Import> ResolveImport (
			MSBuildFileEvaluationContext fileContext,
			string thisFilePath,
			string importExpr,
			string sdk)
		{
			//FIXME: add support for MSBuildUserExtensionsPath, the context does not currently support it
			if (importExpr.IndexOf ("$(MSBuildUserExtensionsPath)", StringComparison.OrdinalIgnoreCase) > -1) {
				yield break;
			}

			//TODO: can we we re-use this context? the propvals may change between evaluations
			var context = new MSBuildCollectedValuesEvaluationContext (fileContext, PropertyCollector);

			bool foundAny = false;
			bool isWildcard = false;

			foreach (var filename in context.EvaluatePathWithPermutation (importExpr, Path.GetDirectoryName (thisFilePath))) {
				if (string.IsNullOrEmpty (filename)) {
					continue;
				}

				//dedup
				if (!ImportedFiles.Add (filename)) {
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

					string[] files;
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
							wildImport = GetCachedOrParse (importExpr, f, sdk, File.GetLastWriteTimeUtc (f));
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
					import = GetCachedOrParse (importExpr, filename, sdk, fi.LastWriteTimeUtc);
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
				if (PreviousRootDocument == null && failedImports.Add (importExpr)) {
					LoggingService.LogDebug ($"Could not resolve MSBuild import '{importExpr}'");
				}
			}
		}

		internal MSBuildImportResolver CreateImportResolver (string filename) => new MSBuildImportResolver (this, filename);

		static readonly HashSet<string> failedImports = new HashSet<string> ();

		public string GetSdkPath (MSBuildDocument doc, string sdk, TextSpan loc)
		{
			if (!SdkReference.TryParse (sdk, out SdkReference sdkRef)) {
				string parseErrorMsg = $"Could not parse SDK '{sdk}'";
				LoggingService.LogError (parseErrorMsg);
				if (doc.IsToplevel) {
					AddError (parseErrorMsg);
				}
				return null;
			}

			try {
				var sdkPath = RuntimeInformation.GetSdkPath (sdkRef, ProjectPath, null);
				if (sdk != null) {
					return sdkPath;
				}
			} catch (Exception ex) {
				LoggingService.LogError ("Error in SDK resolver", ex);
				return null;
			}

			string notFoundMsg = $"Did not find SDK '{sdk}'";
			LoggingService.LogError (notFoundMsg);
			if (doc.IsToplevel) {
				AddError (notFoundMsg);
			}
			return null;

			void AddError (string msg) => doc.Errors.Add (new XmlDiagnosticInfo (DiagnosticSeverity.Error, msg, loc));
		}
	}
}
