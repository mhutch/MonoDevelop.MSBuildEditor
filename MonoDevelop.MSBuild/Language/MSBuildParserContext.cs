// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP
#nullable enable
#endif

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

using Microsoft.Extensions.Logging;

using MonoDevelop.MSBuild.Analysis;
using MonoDevelop.MSBuild.Evaluation;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.MSBuild.SdkResolution;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Logging;
using MonoDevelop.Xml.Parser;

namespace MonoDevelop.MSBuild.Language
{
	// represents the context for a parse operation
	partial class MSBuildParserContext
	{
		public MSBuildRootDocument RootDocument { get; }
		public MSBuildRootDocument? PreviousRootDocument { get; }
		public HashSet<string> ImportedFiles { get; }
		public string? ProjectPath { get; }
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
			MSBuildRootDocument? previous,
			HashSet<string> importedFiles,
			string? projectPath,
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
			public override readonly string ToString () => $"Import: {Import.Filename}";
		}

		public Import ParseImport (Import import)
		{
			if (import.Filename is not string filename) {
				throw new ArgumentException ("Import is not resolved", nameof (import));
			}

			Token.ThrowIfCancellationRequested ();

			using var scope = Logger.BeginScope (new ImportLogScope (import));

			var xmlParser = new XmlTreeParser (new XmlRootState ());
			ITextSource textSource;
			XDocument doc;
			try {
				textSource = new StringTextSource (File.ReadAllText (filename));
				(doc, _) = xmlParser.Parse (textSource.CreateReader (), Token);
			} catch (Exception ex) when (IsNotCancellation (ex)) {
				LogUnhandledXmlParserError (Logger, ex);
				return import;
			}

			import.Document = new MSBuildDocument (filename, false);
			import.Document.Build (doc, this);
			try {
				import.Document.Schema = SchemaProvider.GetSchema (filename, import.Sdk, Logger);
			} catch (Exception ex) {
				LogErrorLoadingSchema (Logger, ex);
			}

			return import;
		}

		public Import GetCachedOrParse (string importExpr, string resolvedFilename, string? sdk, SdkInfo? resolvedSdk, DateTime mtimeUtc, bool isImplicitImport = false)
		{
			var oldDoc = PreviousRootDocument;
			if (oldDoc != null && oldDoc.resolvedImportsMap.TryGetValue (resolvedFilename ?? importExpr, out Import? oldImport) && oldImport.TimeStampUtc == mtimeUtc && oldImport.IsImplicitImport == isImplicitImport) {
				//TODO: check mtimes of descendent imports too
				return oldImport;
			}
			//TODO: guard against cyclic imports
			return ParseImport (new Import (importExpr, sdk, resolvedFilename, resolvedSdk, mtimeUtc, isImplicitImport));
		}

		internal MSBuildImportResolver CreateImportResolver (string? filename)
		{
			if (filename == ProjectPath) {
				return new MSBuildImportResolver (this, filename, RootDocument.FileEvaluationContext);
			}
			return new MSBuildImportResolver (this, filename);
		}

		static readonly HashSet<string> failedImports = new HashSet<string> ();

		/// <summary>
		/// Tries to parse an SDK reference from the Project Sdk attribute. Logs failure to the context and adds
		/// an error to the document if it cannot be parsed.
		/// </summary>
		public bool TryParseSdkReferenceFromProjectSdk (MSBuildDocument doc, string sdkReference, TextSpan loc, out MSBuildSdkReference parsedReference)
		{
			if (MSBuildSdkReference.TryParse (sdkReference, out parsedReference)) {
				return true;
			}

			LogCouldNotParseSdk (Logger, sdkReference);
			if (doc.IsTopLevel) {
				doc.Diagnostics.Add (CoreDiagnostics.InvalidSdkAttribute, loc, parsedReference);
			}

			return false;
		}

		public SdkInfo? ResolveSdk (MSBuildDocument doc, MSBuildSdkReference sdkReference, TextSpan? unresolvedSdkDiagnosticSpan)
		{
			try {
				// NOTE: Microsoft.Build.Framework.SdkResolver requires the project path to be non-null, so
				// if the file is unsaved, construct a placeholder name and use that.
				// TODO: Centralize the logic for determining a placeholder name for unsaved files.
				var projectPathForSdkResolution = ProjectPath
					?? Path.Combine (System.Environment.GetFolderPath (System.Environment.SpecialFolder.UserProfile), "Unsaved.proj");

				var sdkInfo = Environment.ResolveSdk (sdkReference, projectPathForSdkResolution, null, Logger);
				if (sdkInfo is not null) {
					return sdkInfo;
				}
			} catch (Exception ex) when (IsNotCancellation (ex)) {
				LogErrorInSdkResolver (Logger, ex);
				return null;
			}

			string sdkReferenceString = sdkReference.ToString ();
			LogDidNotFindSdk (Logger, sdkReferenceString);
			if (doc.IsTopLevel && unresolvedSdkDiagnosticSpan is not null) {
				doc.Diagnostics.Add (CoreDiagnostics.UnresolvedSdk, unresolvedSdkDiagnosticSpan.Value, sdkReferenceString);
			}
			return null;
		}

		[LoggerMessage (EventId = 0, Level = LogLevel.Error, Message = "Unhandled error parsing xml document")]
		static partial void LogUnhandledXmlParserError (ILogger logger, Exception ex);


		[LoggerMessage (EventId = 1, Level = LogLevel.Error, Message = "Error loading schema for import")]
		static partial void LogErrorLoadingSchema (ILogger logger, Exception ex);

		internal void LogErrorEvaluatingImportWildcardCandidate (Exception ex, string candidateFilename)
			=> LogErrorEvaluatingImportWildcardCandidate (Logger, ex, candidateFilename);


		[LoggerMessage (EventId = 2, Level = LogLevel.Debug, Message = "Error evaluating import wildcard candidate '{candidateFilename}'")]
		static partial void LogErrorEvaluatingImportWildcardCandidate (ILogger logger, Exception ex, UserIdentifiableFileName candidateFilename);

		internal void LogErrorReadingImportWildcardCandidate (Exception ex, string candidateFilename)
			=> LogErrorReadingImportWildcardCandidate (Logger, ex, candidateFilename);


		[LoggerMessage (EventId = 3, Level = LogLevel.Debug, Message = "Error reading import wildcard candidate '{candidateFilename}'")]
		static partial void LogErrorReadingImportWildcardCandidate (ILogger logger, Exception ex, UserIdentifiableFileName candidateFilename);

		internal void LogErrorReadingImportCandidate (Exception ex, string candidateFilename)
			=> LogErrorReadingImportCandidate (Logger, ex, candidateFilename);


		[LoggerMessage (EventId = 4, Level = LogLevel.Warning, Message = "Error reading import candidate '{candidateFilename}'")]
		static partial void LogErrorReadingImportCandidate (ILogger logger, Exception ex, UserIdentifiableFileName candidateFilename);

		internal void LogCouldNotResolveImport (string importExpr)
		{
			if (PreviousRootDocument == null && failedImports.Add (importExpr)) {
				LogCouldNotResolveImport (Logger, importExpr);
			}
		}

		[LoggerMessage (EventId = 5, Level = LogLevel.Debug, Message = "Could not resolve import '{importExpr}'")]
		static partial void LogCouldNotResolveImport (ILogger logger, UserIdentifiable<string> importExpr);


		[LoggerMessage (EventId = 6, Level = LogLevel.Debug, Message = "Could not parse SDK reference '{sdk}'")]
		static partial void LogCouldNotParseSdk (ILogger logger, UserIdentifiable<string> sdk);


		[LoggerMessage (EventId = 7, Level = LogLevel.Error, Message = "Error in SDK resolver")]
		static partial void LogErrorInSdkResolver (ILogger logger, Exception ex);


		[LoggerMessage (EventId = 8, Level = LogLevel.Debug, Message = "Did not find SDK '{sdk}'")]
		static partial void LogDidNotFindSdk (ILogger logger, UserIdentifiable<string> sdk);
	}
}
