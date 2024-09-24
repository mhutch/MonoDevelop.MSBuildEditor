// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.LanguageServer;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CommonLanguageServerProtocol.Framework;

using MonoDevelop.MSBuild.Editor.LanguageServer.Workspace;
using MonoDevelop.Xml.Parser;

namespace MonoDevelop.MSBuild.Editor.LanguageServer;

partial class LspXmlParserService : ILspService
{
    readonly LspEditorWorkspace workspace;
    readonly ILspLogger logger;

    readonly Dictionary<DocumentId, LspXmlParser> parsers = new();

    public LspXmlParserService(ILspLogger logger, LspEditorWorkspace workspace)
    {
        this.workspace = workspace;
        this.logger = logger;

        // these should be fired serially, guaranteeing serial access to the dictionary
        workspace.DocumentOpened += OnDocumentOpened;
        workspace.DocumentChanged += OnDocumentChanged;
        workspace.DocumentClosed += OnDocumentClosed;

        foreach (var openDoc in workspace.OpenDocuments)
        {
            OnDocumentOpened(openDoc.CurrentState);
        }
    }

    public XmlRootState StateMachine { get; } = new();

    void OnDocumentOpened(object? sender, EditorDocumentEventArgs e) => OnDocumentOpened(e.Document);

    void OnDocumentOpened(EditorDocumentState document)
    {
        var parser = new LspXmlParser(logger, document.Id, this);
        parsers.Add(document.Id, parser);
        parser.StartProcessing(document);
    }

    void OnDocumentChanged(object? sender, EditorDocumentChangedEventArgs e)
    {
        var parser = parsers[e.DocumentId];
        parser.StartProcessing(e.Document);
    }

    void OnDocumentClosed(object? sender, EditorDocumentEventArgs e)
    {
        if(parsers.Remove(e.DocumentId, out var parser))
        {
            parser.Dispose();
        }
    }

    /// <summary>Gets a task representing a parse result for the specified document state. It may be completed or running.</summary>
    /// <remarks>Returns false if the document was closed</remarks>
    public bool TryGetParseResult(EditorDocumentState document, [NotNullWhen(true)] out Task<XmlParseResult>? task, CancellationToken cancellationToken = default)
    {
        if (parsers.TryGetValue(document.Id, out var parser))
        {
            task = parser.GetOrProcessAsync(document, CancellationToken.None);
            return true;
        }

        task = null;
        return false;
    }

    /// <summary>Gets the last completed parse result for the specified document. It may be newer than the specified document state.</summary>
    /// <remarks>Returns false if the document has not parsed successfully or if the document was closed</remarks>
    public bool TryGetCompletedParseResult(EditorDocumentState document, [NotNullWhen(true)] out XmlParseResult? lastSuccessfulResult)
    {
        if(parsers.TryGetValue(document.Id, out var parser))
        {
            lastSuccessfulResult = parser.LastOutput;
            return lastSuccessfulResult is not null;
        }

        lastSuccessfulResult = null;
        return false;
    }

    public XmlSpineParser? GetSpineParser(LinePosition point, EditorDocumentState document, CancellationToken token = default)
    {
        if(parsers.TryGetValue(document.Id, out var parser))
        {
            return parser.GetSpineParser(point, document.Text.Text, token);
        }

        return null;
    }

    public void SubscribeParseNotification(DocumentId documentId, Action<XmlParseResult> handler)
    {
        var parser = parsers[documentId];
        parser.ParseCompleted += (e, a) => handler(a.Result);

        if(parser.LastOutput is { } result)
        {
               handler(result); 
        }
    }

    internal void OnParseCompleted(XmlParseResult result)
    {
        ParseCompleted?.Invoke(this, new ParseCompletedEventArgs<XmlParseResult>(result.DocumentState.Id, result));
    }

    public event EventHandler<ParseCompletedEventArgs<XmlParseResult>>? ParseCompleted;
}
