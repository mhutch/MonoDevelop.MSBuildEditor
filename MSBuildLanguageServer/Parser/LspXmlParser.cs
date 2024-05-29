// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CommonLanguageServerProtocol.Framework;

using MonoDevelop.MSBuild.Editor.LanguageServer.Parser;
using MonoDevelop.MSBuild.Editor.LanguageServer.Workspace;
using MonoDevelop.Xml.Analysis;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Editor.Parsing;
using MonoDevelop.Xml.Parser;

namespace MonoDevelop.MSBuild.Editor.LanguageServer;

partial class LspXmlParserService
{
    class LspXmlParser : BackgroundProcessor<EditorDocumentState, XmlParseResult>
    {
        readonly ILspLogger logger;
        readonly LspXmlParserService parserService;

        public LspXmlParser(ILspLogger logger, DocumentId documentId, LspXmlParserService parserService)
            : base(NullBackgroundParseService.Instance)
        {
            DocumentId = documentId;
            this.parserService = parserService;
            this.logger = logger;
            StateMachine = CreateParserStateMachine();
        }

        protected XmlRootState StateMachine { get; }

        public DocumentId DocumentId { get; }

        protected virtual XmlRootState CreateParserStateMachine() => new();

        protected override Task<XmlParseResult> StartOperationAsync(
            EditorDocumentState input,
            XmlParseResult? previousOutput,
            EditorDocumentState? previousInput,
            CancellationToken token)
        {
            return Task.Run(() => {
                var parser = new XmlTreeParser(StateMachine);
                var text = input.Text.Text;
                var length = text.Length;
                for(int i = 0; i < length; i++)
                {
                    parser.Push(text[i]);
                    token.ThrowIfCancellationRequested();
                }
                var (doc, diagnostics) = parser.EndAllNodes();
                return new XmlParseResult(input, doc, diagnostics, StateMachine);
            }, token);
        }

        protected override void OnUnhandledParseError(Exception ex)
        {
            logger.LogException(ex, "Unhandled XML parser error");
        }

        protected override void OnOperationCompleted(EditorDocumentState input, XmlParseResult output)
        {
            parserService.OnParseCompleted(output);
        }

        public event EventHandler<ParseCompletedEventArgs<XmlParseResult>>? ParseCompleted;

        protected override int CompareInputs(EditorDocumentState a, EditorDocumentState b) => a.Text.Version.CompareTo(b.Text.Version);

        public XmlSpineParser GetSpineParser(LinePosition point, SourceText text, CancellationToken token = default)
            => LspXmlParserService.GetSpineParser(StateMachine, LastOutput, point, text, token);

        internal new void StartProcessing(EditorDocumentState document)
        {
            base.StartProcessing(document);
        }
    }

    internal static XmlSpineParser GetSpineParser(XmlRootState stateMachine, XmlParseResult? baseline, LinePosition point, SourceText text, CancellationToken token = default)
    {
        XmlSpineParser? parser = null;

        var offset = point.ToOffset (text);

        if(baseline is not null)
        {
            var startPos = Math.Min(offset, MaximumCompatiblePosition(baseline.Text, text));
            if(startPos > 0)
            {
                parser = XmlSpineParser.FromDocumentPosition(stateMachine, baseline.XDocument, startPos);
            }
        }

        if(parser == null)
        {
            parser = new XmlSpineParser(stateMachine);
        }

        var end = Math.Min(offset, text.Length);
        for(int i = parser.Position; i < end; i++)
        {
            token.ThrowIfCancellationRequested();
            parser.Push(text[i]);
        }

        return parser;


        static int MaximumCompatiblePosition(SourceText oldText, SourceText newText)
        {
            var changes = newText.GetChangeRanges(oldText);
            return changes.Count == 0 ? newText.Length : changes[0].Span.Start;
        }
    }
}

class XmlParseResult(EditorDocumentState documentState, XDocument xDocument, IReadOnlyList<XmlDiagnostic>? diagnostics, XmlRootState stateMachine)
{
    public EditorDocumentState DocumentState => documentState;
    public XDocument XDocument => xDocument;
    public IReadOnlyList<XmlDiagnostic>? Diagnostics => diagnostics;
    public string FilePath => documentState.FilePath;
    public SourceText Text => documentState.Text.Text;
    public VersionStamp Version => documentState.Text.Version;

    public XmlSpineParser GetSpineParser(LinePosition point, CancellationToken token = default)
        => LspXmlParserService.GetSpineParser(stateMachine, this, point, Text, token);
}
