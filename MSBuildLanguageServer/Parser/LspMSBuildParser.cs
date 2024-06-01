// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.LanguageServer;

using MonoDevelop.MSBuild.Editor.LanguageServer.Parser;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.Xml.Editor.Parsing;

namespace MonoDevelop.MSBuild.Editor.LanguageServer;

partial class LspMSBuildParserService
{

    // based on MonoDevelop.MSBuild.Editor.Completion.MSBuildBackgroundParser
    class LspMSBuildParser : BackgroundProcessor<XmlParseResult, MSBuildParseResult>, ILspService
    {
        //readonly MSBuildAnalyzerDriver analyzerDriver;
        readonly LspMSBuildParserService service;

        public LspMSBuildParser(LspMSBuildParserService service, DocumentId documentId)
            : base(NullBackgroundParseService.Instance)
        {
            this.service = service;
            service.xmlParserService.SubscribeParseNotification(documentId, OnXmlParse);
            /*

            analyzerDriver = new MSBuildAnalyzerDriver (this.logger);*/
        }

        void OnXmlParse(XmlParseResult result) => StartProcessing(result);

        protected override Task<MSBuildParseResult> StartOperationAsync(
            XmlParseResult input,
            MSBuildParseResult? previousOutput,
            XmlParseResult? previousInput,
            CancellationToken token)
        {
            return Task.Run(() => {
                var oldDoc = previousOutput?.MSBuildDocument;

                MSBuildRootDocument doc;
                try
                {
                    doc = MSBuildRootDocument.Parse(
                        input.Text.GetTextSource(),
                        input.FilePath,
                        oldDoc,
                        service.schemaProvider,
                        service.msbuildEnvironment,
                        service.taskMetadataBuilder,
                        service.extLogger,
                        token);
                } catch(Exception ex) when(!(ex is OperationCanceledException && token.IsCancellationRequested))
                {
                    LogUnhandledParserError(ex);
                    doc = MSBuildRootDocument.Empty;
                }

                return new MSBuildParseResult(doc, input);
            }, token);
        }

        void LogUnhandledParserError(Exception ex)
            => service.logger.LogException(ex, "Unhandled error in MSBuild parser");

        protected override void OnOperationCompleted(XmlParseResult input, MSBuildParseResult output)
        {
            service.OnParseCompleted(output);
            ParseCompleted?.Invoke(this, new ParseCompletedEventArgs<MSBuildParseResult>(output.DocumentId, output));
        }

        protected override int CompareInputs(XmlParseResult a, XmlParseResult b)
            => a.Version.CompareTo(b.Version);

        public event EventHandler<ParseCompletedEventArgs<MSBuildParseResult>>? ParseCompleted;
    }
}

record MSBuildParseResult(MSBuildRootDocument MSBuildDocument, XmlParseResult XmlParseResult)
{
    public DocumentId DocumentId => XmlParseResult.DocumentState.Id;
}

class ParseCompletedEventArgs<TParseResult>(DocumentId documentId, TParseResult result) : EventArgs
{
    public DocumentId DocumentId { get; } = documentId;
    public TParseResult Result { get; } = result;
}
