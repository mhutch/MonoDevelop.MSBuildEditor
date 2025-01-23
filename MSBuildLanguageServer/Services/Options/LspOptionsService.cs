// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.LanguageServer;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.Extensions.Logging;
using MonoDevelop.MSBuild.Editor.LanguageServer.Workspace;
using MonoDevelop.MSBuild.Editor.Options;
using MonoDevelop.Xml.Options;

using Roslyn.LanguageServer.Protocol;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Services.Options;

/// <summary>
/// Service that provides options for a document
/// </summary>
partial class LspDocumentOptionsService : ILspService
{
    readonly ILspLogger logger;
    readonly ILogger extLogger;
    readonly LspEditorWorkspace workspace;
    readonly IGlobalOptionService globalOptionService;

    readonly Dictionary<DocumentId, EditorConfigOptionsReader> editorConfigOptionCache = new();
    readonly EditorConfigOptionsService editorConfigOptionsService = new();

    public LspDocumentOptionsService(
        ILspLogger logger,
        LspEditorWorkspace workspace,
        IGlobalOptionService globalOptionService)
    {
        this.logger = logger;
        extLogger = logger.ToILogger();
        this.workspace = workspace;
        this.globalOptionService = globalOptionService;

        workspace.DocumentClosed += OnDocumentClosed;
    }

    public IOptionsReader GetDocumentOptions(TextDocumentIdentifier textDocument)
    {
        var editorDoc = workspace.GetEditorDocument(textDocument.Uri);
        return GetDocumentOptions(editorDoc);
    }

    public IOptionsReader GetDocumentOptions(LspEditorDocument document)
    {
        var editorConfigOptions = editorConfigOptionCache.GetOrAdd(document.Id, () => {
            // TODO: read editorconfig files asynchronously
            // TODO: set up filesystem watchers for the editorconfig files
            // TODO: if the editorconfig files are open but unsaved, use the text from the editor
            return editorConfigOptionsService.GetOptionsForSourceFile(document.FilePath);
        });

        var documentOptions = new LspDocumentOptionsReader(editorConfigOptions, globalOptionService);
        return documentOptions;
    }

    void OnDocumentClosed(object? sender, EditorDocumentEventArgs e)
    {
        if(editorConfigOptionCache.Remove(e.DocumentId, out var optionSet))
        {
            optionSet.Dispose();
        }
    }
}

class LspDocumentOptionsReader(EditorConfigOptionsReader editorConfigOptions, IGlobalOptionService globalOptions)
    : IOptionsReader
{
    public bool TryGetOption<T>(Option<T> option, out T? value)
    {
        if (editorConfigOptions.TryGetOption(option, out value))
        {
            return true;
        }

        if (globalOptions.TryGetOption(option, out value))
        {
            return true;
        }

        value = default;
        return false;
    }
}