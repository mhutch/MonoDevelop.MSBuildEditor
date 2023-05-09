// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Text;

using MonoDevelop.MSBuild.Analysis;
using MonoDevelop.MSBuild.Language;

using MonoDevelop.Xml.Editor;
using MonoDevelop.Xml.Editor.Completion;
using MonoDevelop.Xml.Editor.Logging;

namespace MonoDevelop.MSBuild.Editor.Completion
{
	class MSBuildBackgroundParser : BackgroundProcessor<XmlParseResult,MSBuildParseResult>
	{
		readonly ILogger logger;
		readonly MSBuildParserProvider provider;
		string filepath;

		//FIXME: move this to a lower priority BackgroundProcessor
		MSBuildAnalyzerDriver analyzerDriver;

		public XmlBackgroundParser XmlParser { get; }

		public MSBuildBackgroundParser (ITextBuffer buffer, MSBuildParserProvider provider)
		{
			XmlParser = provider.XmlParserProvider.GetParser (buffer);
			XmlParser.ParseCompleted += XmlParseCompleted;

			if (buffer.Properties.TryGetProperty<ITextDocument> (typeof (ITextDocument), out var doc)) {
				filepath = doc.FilePath;
				doc.FileActionOccurred += OnFileAction;
			}

			logger = provider.LoggerFactory.CreateLogger<MSBuildBackgroundParser> (buffer);
			analyzerDriver = new MSBuildAnalyzerDriver ();
			analyzerDriver.AddBuiltInAnalyzers ();
			this.provider = provider;
		}

		void OnFileAction (object sender, TextDocumentFileActionEventArgs e)
		{
			if (e.FileActionType == FileActionTypes.DocumentRenamed) {
				filepath = ((ITextDocument)sender).FilePath;
			}
		}

		void XmlParseCompleted (object sender, ParseCompletedEventArgs<XmlParseResult> e)
		{
			StartProcessing (e.ParseResult);
		}

		protected override Task<MSBuildParseResult> StartOperationAsync (
			XmlParseResult input,
			MSBuildParseResult previousOutput,
			XmlParseResult previousInput,
			CancellationToken token)
		{
			return Task.Run (() => {
				var oldDoc = previousOutput?.MSBuildDocument;

				MSBuildRootDocument doc;
				try {
					doc = MSBuildRootDocument.Parse (
						input.TextSnapshot.GetTextSource (),
						filepath,
						oldDoc,
						provider.SchemaProvider,
						provider.MSBuildEnvironment,
						provider.TaskMetadataBuilder,
						logger,
						token);

					var analyzerDiagnostics = analyzerDriver.Analyze (doc, true, token);
					doc.Diagnostics.Clear ();
					doc.Diagnostics.AddRange (analyzerDiagnostics);
				}
				catch (Exception ex) when (!(ex is OperationCanceledException && token.IsCancellationRequested)) {
					LoggingService.LogError ("Unhandled error in MSBuild parser", ex);
					doc = MSBuildRootDocument.Empty;
				}
				// for some reason the VS debugger thinks cancellation exceptions aren't handled?
				catch (OperationCanceledException) when (token.IsCancellationRequested) {
					return null;
				}

				return new MSBuildParseResult (doc, doc.Diagnostics, input.TextSnapshot);
			}, token);
		}

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

		public event EventHandler<ParseCompletedEventArgs<MSBuildParseResult>> ParseCompleted;
	}
}