// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.LanguageServer;
using Microsoft.CodeAnalysis.LanguageServer.Handler;
using Microsoft.CodeAnalysis.Text;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Workspace;

class LspEditorWorkspace : ILspService, IDocumentChangeTracker
{
    // RequestExecutionQueue guarantees serial access to this
    readonly Dictionary<Uri, LspEditorDocument> documents = new();
    ImmutableDictionary<Uri, (SourceText Text, string LanguageId)> _trackedDocuments
        = ImmutableDictionary<Uri, (SourceText Text, string LanguageId)>.Empty;

    public LspEditorDocument GetEditorDocument(Uri documentUri)
    {
        if (documents.TryGetValue(documentUri, out var doc)) {
            return doc;
        }
        throw new InvalidOperationException("Document not open");
    }

    public ValueTask StartTrackingAsync(Uri documentUri, SourceText initialText, string languageId, CancellationToken cancellationToken)
    {
        if(documents.TryGetValue(documentUri, out var document))
        {
            throw new InvalidOperationException("Already tracking document");

        }

        var documentId = DocumentId.CreateNewId(ProjectId.CreateNewId());
        document = new LspEditorDocument (documentId, ProtocolConversions.GetDocumentFilePathFromUri (documentUri), initialText);
        documents.Add(documentUri, document);
        _trackedDocuments = _trackedDocuments.Add(documentUri, (initialText, languageId));

        DocumentOpened?.Invoke(this, new EditorDocumentEventArgs(document.CurrentState));

        return ValueTask.CompletedTask;
    }

    public void UpdateTrackedDocument(Uri documentUri, SourceText text)
    {
        var doc = GetEditorDocument(documentUri);
        var oldState = doc.CurrentState;
        doc.UpdateText(text);
        var newState = doc.CurrentState;

        _trackedDocuments = _trackedDocuments.SetItem(documentUri, (text, _trackedDocuments[documentUri].LanguageId));

        DocumentChanged?.Invoke(this, new EditorDocumentChangedEventArgs(newState, oldState));
    }

    public ValueTask StopTrackingAsync(Uri documentUri, CancellationToken cancellationToken)
    {
        _trackedDocuments = _trackedDocuments.Remove(documentUri);

        if (!documents.Remove(documentUri, out var document))
        {
            throw new InvalidOperationException("Document not open");
        }

        DocumentClosed?.Invoke(this, new EditorDocumentEventArgs(document.CurrentState));

        return ValueTask.CompletedTask;
    }

    public ImmutableDictionary<Uri, (SourceText Text, string LanguageId)> GetTrackedLspText() => _trackedDocuments;

    public IReadOnlyCollection<LspEditorDocument> OpenDocuments => documents.Values.ToArray();

    public event EventHandler<EditorDocumentEventArgs>? DocumentOpened;
    public event EventHandler<EditorDocumentChangedEventArgs>? DocumentChanged;
    public event EventHandler<EditorDocumentEventArgs>? DocumentClosed;
}

class EditorDocumentChangedEventArgs(EditorDocumentState newDocument, EditorDocumentState oldDocument)
    : EditorDocumentEventArgs(newDocument)
{
    public EditorDocumentState OldState { get; } = oldDocument;
}

class EditorDocumentEventArgs(EditorDocumentState document) : EventArgs
{
    public EditorDocumentState Document { get; } = document;
    public DocumentId DocumentId { get; } = document.Id;
}