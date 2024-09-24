// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Text;

using MonoDevelop.MSBuild.Analysis;
using MonoDevelop.MSBuild.Language;

using MonoDevelop.Xml.Editor;
using MonoDevelop.Xml.Editor.Logging;
using MonoDevelop.Xml.Editor.Parsing;

namespace MonoDevelop.MSBuild.Editor.Completion
{
	partial class MSBuildBackgroundParser : BackgroundProcessor<XmlParseResult,MSBuildParseResult>
	{
		readonly ILogger logger;
		readonly MSBuildParserProvider provider;
		string? filePath;

		//FIXME: move this to a lower priority BackgroundProcessor
		MSBuildAnalyzerDriver analyzerDriver;

		public XmlBackgroundParser XmlParser { get; }

		public MSBuildBackgroundParser (ITextBuffer buffer, MSBuildParserProvider provider, IBackgroundParseService parseService) : base (parseService)
		{
			XmlParser = provider.XmlParserProvider.GetParser (buffer);
			XmlParser.ParseCompleted += XmlParseCompleted;

			if (buffer.Properties.TryGetProperty<ITextDocument> (typeof (ITextDocument), out var doc)) {
				filePath = doc.FilePath;
				doc.FileActionOccurred += OnFileAction;
			}

			logger = provider.LoggerFactory.CreateLogger<MSBuildBackgroundParser> (buffer);

			var analyzerDriverLogger = provider.LoggerFactory.GetLogger<MSBuildAnalyzerDriver> (buffer);
			analyzerDriver = new MSBuildAnalyzerDriver (analyzerDriverLogger);
			analyzerDriver.AddBuiltInAnalyzers ();
			this.provider = provider;
		}

		void OnFileAction (object? sender, TextDocumentFileActionEventArgs e)
		{
			if (e.FileActionType == FileActionTypes.DocumentRenamed && sender is ITextDocument doc) {
				filePath = doc.FilePath;
			}
		}

		void XmlParseCompleted (object? sender, ParseCompletedEventArgs<XmlParseResult> e)
		{
			StartProcessing (e.ParseResult);
		}

		protected override Task<MSBuildParseResult> StartOperationAsync (
			XmlParseResult input,
			MSBuildParseResult? previousOutput,
			XmlParseResult? previousInput,
			CancellationToken token)
		{
			return Task.Run (() => {
				var oldDoc = previousOutput?.MSBuildDocument;

				MSBuildRootDocument doc;
				try {
					doc = MSBuildRootDocument.Parse (
						input.TextSnapshot.GetTextSource (),
						filePath,
						oldDoc,
						provider.SchemaProvider,
						provider.MSBuildEnvironment,
						provider.TaskMetadataBuilder,
						logger,
						token);

					if (doc.ProjectElement is not null) {
						var analyzerDiagnostics = analyzerDriver.Analyze (doc, true, token);
						doc.Diagnostics.Clear ();
						doc.Diagnostics.AddRange (analyzerDiagnostics);
					}
				}
				catch (Exception ex) when (!(ex is OperationCanceledException && token.IsCancellationRequested)) {
					LogUnhandledParserError (logger, ex);
					doc = MSBuildRootDocument.Empty;
				}

				return new MSBuildParseResult (doc, input.TextSnapshot);
			}, token);
		}

		[LoggerMessage (EventId = 0, Level = LogLevel.Error, Message = "Unhandled error in MSBuild parser")]
		static partial void LogUnhandledParserError (ILogger logger, Exception ex);

		protected override int CompareInputs (XmlParseResult a, XmlParseResult b)
			=> a.TextSnapshot.Version.VersionNumber.CompareTo (b.TextSnapshot.Version.VersionNumber);

		public async Task<MSBuildParseResult> GetOrProcessAsync (ITextSnapshot snapshot, CancellationToken token)
		{
			var xmlResult = await XmlParser.GetOrProcessAsync (snapshot, token);
			return await GetOrProcessAsync (xmlResult, token);
		}

		protected override void OnOperationCompleted (XmlParseResult input, MSBuildParseResult output)
		{
			ParseCompleted?.Invoke (this, new ParseCompletedEventArgs<MSBuildParseResult> (output, output.Snapshot));
		}

		public event EventHandler<ParseCompletedEventArgs<MSBuildParseResult>>? ParseCompleted;
	}
}