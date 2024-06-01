// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.LanguageServer;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.Extensions.Logging;

using MonoDevelop.MSBuild.Editor.LanguageServer.Workspace;
using MonoDevelop.MSBuild.Schema;

namespace MonoDevelop.MSBuild.Editor.LanguageServer;

partial class LspMSBuildParserService : ILspService
{
    readonly ILspLogger logger;
    readonly ILogger extLogger;
    readonly LspEditorWorkspace workspace;
    readonly LspXmlParserService xmlParserService;
    readonly IMSBuildEnvironment msbuildEnvironment;
    readonly MSBuildSchemaProvider schemaProvider;
    readonly ITaskMetadataBuilder taskMetadataBuilder;

    Dictionary<DocumentId, LspMSBuildParser> parsers = new();

    public LspMSBuildParserService(
        ILspLogger logger,
        LspEditorWorkspace workspace,
        LspXmlParserService xmlParserService,
        IMSBuildEnvironment msbuildEnvironment,
        MSBuildSchemaProvider schemaProvider,
        ITaskMetadataBuilder taskMetadataBuilder)
    {
        this.logger = logger;
        this.extLogger = logger.ToILogger();
        this.workspace = workspace;
        this.xmlParserService = xmlParserService;
        this.msbuildEnvironment = msbuildEnvironment;
        this.schemaProvider = schemaProvider;
        this.taskMetadataBuilder = taskMetadataBuilder;

        // as this service takes an LspXmlParserService argument, the XmlParserService will register its workspace events before this does
        // so by the time our OnDocumentOpened fires, the XmlParserService's OnDocumentOpened will have fired already
        workspace.DocumentOpened += OnDocumentOpened;
        workspace.DocumentClosed += OnDocumentClosed;

        foreach(var openDoc in workspace.OpenDocuments)
        {
            OnDocumentOpened(openDoc.CurrentState);
        }
    }
    void OnDocumentOpened(object? sender, EditorDocumentEventArgs e) => OnDocumentOpened(e.Document);

    void OnDocumentOpened(EditorDocumentState document)
    {
        var parser = new LspMSBuildParser(this, document.Id);
        parsers.Add(document.Id, parser);
    }

    void OnDocumentClosed(object? sender, EditorDocumentEventArgs e)
    {
        if(parsers.Remove(e.DocumentId, out var parser))
        {
            parser.Dispose();
        }
    }

    /// <remarks>Returns false if the document was closed</remarks>
    public bool TryGetParseResult(EditorDocumentState document, [NotNullWhen(true)] out Task<MSBuildParseResult>? task, CancellationToken cancellationToken = default)
    {
        if(parsers.TryGetValue(document.Id, out var parser))
        {
            if (!xmlParserService.TryGetParseResult(document, out Task<XmlParseResult>? xmlTask, cancellationToken))
            {
                task = null;
                return false;
            }
            task = xmlTask.ContinueWith(xt => parser.GetOrProcessAsync(xt.Result, cancellationToken), cancellationToken, TaskContinuationOptions.None, TaskScheduler.Default).Unwrap();
            return true;
        }

        task = null;
        return false;
    }

    public void SubscribeParseNotification(DocumentId documentId, Action<MSBuildParseResult> handler)
    {
        var parser = parsers[documentId];
        parser.ParseCompleted += (e, a) => handler(a.Result);

        if(parser.LastOutput is { } result)
        {
            handler(result);
        }
    }

    internal void OnParseCompleted(MSBuildParseResult result)
    {
        ParseCompleted?.Invoke(this, new ParseCompletedEventArgs<MSBuildParseResult>(result.DocumentId, result));
    }

    public event EventHandler<ParseCompletedEventArgs<MSBuildParseResult>>? ParseCompleted;
}
