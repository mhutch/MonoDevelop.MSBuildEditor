// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

using Microsoft.Extensions.Logging;

using MonoDevelop.MSBuild.Analysis;
using MonoDevelop.MSBuild.Evaluation;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.MSBuild.SdkResolution;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Parser;

using SdkReference = Microsoft.Build.Framework.SdkReference;

namespace MonoDevelop.MSBuild.Language
{
	// represents the context for a parse operation
	partial class MSBuildParserContext
	{
		public MSBuildRootDocument RootDocument { get; }
		public MSBuildRootDocument PreviousRootDocument { get; }
		public HashSet<string> ImportedFiles { get; }
		public string ProjectPath { get; }
		public PropertyValueCollector PropertyCollector { get; }
		public ITaskMetadataBuilder TaskBuilder { get; }
		public MSBuildSchemaProvider SchemaProvider { get; }
		public CancellationToken Token { get; }
		public MSBuildProjectEvaluationContext ProjectEvaluationContext { get; }
		public IMSBuildEnvironment Environment { get; }
		public ILogger Logger { get; }

		public MSBuildParserContext (
			IMSBuildEnvironment env,
			MSBuildRootDocument doc,
			MSBuildRootDocument previous,
			HashSet<string> importedFiles,
			string projectPath,
			PropertyValueCollector propVals,
			ITaskMetadataBuilder taskBuilder,
			ILogger logger,
			MSBuildSchemaProvider schemaProvider,
			CancellationToken token)
		{
			Environment = env;
			RootDocument = doc;
			PreviousRootDocument = previous;
			ImportedFiles = importedFiles;
			ProjectPath = projectPath;
			PropertyCollector = propVals;
			TaskBuilder = taskBuilder;
			Logger = logger;
			SchemaProvider = schemaProvider;
			Token = token;

			ProjectEvaluationContext = new MSBuildProjectEvaluationContext (env, projectPath, logger);
		}

		public bool IsNotCancellation (Exception ex) => !(ex is OperationCanceledException && Token.IsCancellationRequested);

		record struct ImportLogScope (Import Import)
		{
			public override string ToString () => $"Import: {Import.Filename}";
		}

		public Import ParseImport (Import import)
		{
			Token.ThrowIfCancellationRequested ();

			using var scope = Logger.BeginScope (new ImportLogScope (import));

			var xmlParser = new XmlTreeParser (new XmlRootState ());
			ITextSource textSource;
			XDocument doc;
			try {
				textSource = new StringTextSource (File.ReadAllText (import.Filename));
				(doc, _) = xmlParser.Parse (textSource.CreateReader (), Token);
			} catch (Exception ex) when (IsNotCancellation (ex)) {
				LogUnhandledXmlParserError (Logger, ex);
				return import;
			}

			import.Document = new MSBuildDocument (import.Filename, false);
			import.Document.Build (doc, this);
			try {
				import.Document.Schema = SchemaProvider.GetSchema (import.Filename, import.Sdk, Logger);
			} catch (Exception ex) {
				LogErrorLoadingSchema (Logger, ex);
			}

			return import;
		}

		public Import GetCachedOrParse (string importExpr, string resolvedFilename, string sdk, SdkInfo resolvedSdk, DateTime mtimeUtc, bool isImplicitImport = false)
		{
			var oldDoc = PreviousRootDocument;
			if (oldDoc != null && oldDoc.resolvedImportsMap.TryGetValue (resolvedFilename ?? importExpr, out Import oldImport) && oldImport.TimeStampUtc == mtimeUtc && oldImport.IsImplicitImport == isImplicitImport) {
				//TODO: check mtimes of descendent imports too
				return oldImport;
			}
			//TODO: guard against cyclic imports
			return ParseImport (new Import (importExpr, sdk, resolvedFilename, resolvedSdk, mtimeUtc, isImplicitImport));
		}

		internal IEnumerable<Import> ResolveImport (
			IMSBuildEvaluationContext fileContext,
			string thisFilePath,
			ExpressionNode importExpr,
			string importExprString,
			string sdk,
			SdkInfo resolvedSdk,
			bool isImplicitImport = false)
		{
			//FIXME: add support for MSBuildUserExtensionsPath, the context does not currently support it
			if (importExprString.IndexOf ("$(MSBuildUserExtensionsPath)", StringComparison.OrdinalIgnoreCase) > -1) {
				yield break;
			}

			var evalCtx = new MSBuildCollectedValuesEvaluationContext (fileContext, PropertyCollector);

			bool foundAny = false;
			bool isWildcard = false;

			IList<string> basePaths;
			if (resolvedSdk != null) {
				basePaths = resolvedSdk.Paths;
			} else {
				basePaths = new[] { Path.GetDirectoryName (thisFilePath) };
			}

			foreach (var filename in basePaths.SelectMany(basePath => evalCtx.EvaluatePathWithPermutation (importExpr, basePath))) {
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
					} catch (Exception ex) when (IsNotCancellation (ex)) {
						LogErrorEvaluatingImportWildcardCandidate (Logger, ex, filename);
						continue;
					}

					foreach (var f in files) {
						Import wildImport;
						try {
							wildImport = GetCachedOrParse (importExprString, f, sdk, resolvedSdk, File.GetLastWriteTimeUtc (f));
						} catch (Exception ex) when (IsNotCancellation (ex)) {
							LogErrorReadingImportWildcardCandidate (Logger, ex, f);
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
					import = GetCachedOrParse (importExprString, filename, sdk, resolvedSdk, fi.LastWriteTimeUtc, isImplicitImport);
				} catch (Exception ex) when (IsNotCancellation (ex)) {
					LogErrorReadingImportCandidate (Logger, ex, filename);
					continue;
				}

				foundAny = true;
				yield return import;
				continue;
			}

			//yield a placeholder for tooltips, imports pad etc to query
			if (!foundAny) {
				yield return new Import (importExprString, sdk, null, resolvedSdk, DateTime.MinValue, false);
			}

			// we skip logging for wildcards as these are generally extensibility points that are often unused
			// this is here (rather than being folded into the next condition) for ease of breakpointing
			if (!foundAny && !isWildcard) {
				if (PreviousRootDocument == null && failedImports.Add (importExprString)) {
					LogCouldNotResolveImport (Logger, importExprString);
				}
			}
		}

		internal MSBuildImportResolver CreateImportResolver (string filename)
		{
			if (filename == ProjectPath) {
				return new MSBuildImportResolver (this, filename, RootDocument.FileEvaluationContext);
			}
			return new MSBuildImportResolver (this, filename);
		}

		static readonly HashSet<string> failedImports = new HashSet<string> ();

		public SdkInfo ResolveSdk (MSBuildDocument doc, string sdk, TextSpan loc)
		{
			if (!SdkReference.TryParse (sdk, out SdkReference sdkRef)) {
				LogCouldNotParseSdk (Logger, sdk);
				if (doc.IsToplevel) {
					doc.Diagnostics.Add (CoreDiagnostics.InvalidSdkAttribute, loc, sdk);
				}
				return null;
			}

			try {
				var sdkInfo = Environment.ResolveSdk ((sdkRef.Name, sdkRef.Version, sdkRef.MinimumVersion), ProjectPath, null, Logger);
				if (sdk != null) {
					return sdkInfo;
				}
			} catch (Exception ex) when (IsNotCancellation (ex)) {
				LogErrorInSdkResolver (Logger, ex);
				return null;
			}

			LogDidNotFindSdk (Logger, sdk);
			if (doc.IsToplevel) {
				doc.Diagnostics.Add (CoreDiagnostics.SdkNotFound, loc, sdk);
			}
			return null;
		}

		[LoggerMessage (EventId = 0, Level = LogLevel.Error, Message = "Unhandled error parsing xml document")]
		static partial void LogUnhandledXmlParserError (ILogger logger, Exception ex);


		[LoggerMessage (EventId = 1, Level = LogLevel.Error, Message = "Error loading schema for import")]
		static partial void LogErrorLoadingSchema (ILogger logger, Exception ex);


		[LoggerMessage (EventId = 2, Level = LogLevel.Debug, Message = "Error evaluating import wildcard candidate '{candidateFilename}'")]
		static partial void LogErrorEvaluatingImportWildcardCandidate (ILogger logger, Exception ex, string candidateFilename);


		[LoggerMessage (EventId = 3, Level = LogLevel.Debug, Message = "Error reading import wildcard candidate '{candidateFilename}'")]
		static partial void LogErrorReadingImportWildcardCandidate (ILogger logger, Exception ex, string candidateFilename);


		[LoggerMessage (EventId = 4, Level = LogLevel.Warning, Message = "Error reading import candidate '{candidateFilename}'")]
		static partial void LogErrorReadingImportCandidate (ILogger logger, Exception ex, string candidateFilename);


		[LoggerMessage (EventId = 5, Level = LogLevel.Debug, Message = "Could not resolve import '{importExpr}'")]
		static partial void LogCouldNotResolveImport (ILogger logger, string importExpr);


		[LoggerMessage (EventId = 6, Level = LogLevel.Error, Message = "Could not parse SDK reference '{sdk}'")]
		static partial void LogCouldNotParseSdk (ILogger logger, string sdk);


		[LoggerMessage (EventId = 7, Level = LogLevel.Error, Message = "Error in SDK resolver")]
		static partial void LogErrorInSdkResolver (ILogger logger, Exception ex);


		[LoggerMessage (EventId = 8, Level = LogLevel.Error, Message = "Did not find SDK '{sdk}'")]
		static partial void LogDidNotFindSdk (ILogger logger, string sdk);
	}
}
