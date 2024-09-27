// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.LanguageServer;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.Extensions.Logging;

using MonoDevelop.MSBuild.Editor.LanguageServer.Workspace;
using MonoDevelop.Xml.Options;
using Roslyn.LanguageServer.Protocol;

namespace MonoDevelop.MSBuild.Editor.LanguageServer;

partial class LspOptionsService : ILspService
{
    readonly ILspLogger logger;
    readonly ILogger extLogger;
    readonly LspEditorWorkspace workspace;
    readonly Dictionary<DocumentId, LspOptionSet> optionSets = new();

    public LspOptionsService(
        ILspLogger logger,
        LspEditorWorkspace workspace)
    {
        this.logger = logger;
        this.extLogger = logger.ToILogger();
        this.workspace = workspace;

        workspace.DocumentClosed += OnDocumentClosed;
    }

    public IOptionsReader GetDocumentOptions(TextDocumentIdentifier textDocument)
    {
        var editorDoc = workspace.GetEditorDocument(textDocument.Uri);
        var documentId = editorDoc.Id;
        return GetDocumentOptions(documentId);
    }

    public IOptionsReader GetDocumentOptions(DocumentId documentId)
    {
        return optionSets.GetOrAdd(documentId, () => new LspOptionSet(documentId, this));
    }

    void OnDocumentClosed(object? sender, EditorDocumentEventArgs e)
    {
        if(optionSets.Remove(e.DocumentId, out var optionSet))
        {
            //optionSet.Dispose();
        }
    }
}

class LspOptionSet : IOptionsReader
{
    internal LspOptionSet(DocumentId doc, LspOptionsService parent)
    {
    }

    public bool TryGetOption<T>(Option<T> option, [MaybeNullWhen(false)] out T value)
    {
        value = default!;
        return false;
    }
}
