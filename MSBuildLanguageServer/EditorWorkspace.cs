// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace MonoDevelop.MSBuild.Editor.LanguageServer;

/// <summary>
/// Workspace that tracks documents that are open in the editor
/// </summary>
public class EditorWorkspace : Workspace
{
    readonly object openDocumentsLock = new ();
    readonly Dictionary<DocumentId,EditorDocument> openDocuments = new();

    public EditorWorkspace(HostServices services) : base(services)
    {
    }

    public EditorDocument StartTrackingOpenDocument(string normalizedAbsoluteFilePath)
    {
        var id = GetOrCreateDocumentId(normalizedAbsoluteFilePath);
        var document = new EditorDocument(this, id, null);
        lock(openDocumentsLock) {
            if (!openDocuments.TryAdd(id, document)) {
                throw new ArgumentException("Document is already being tracked");
            }
        }

        if (DocumentOpened is {} handlers) {
            ScheduleTask(() => handlers.Invoke(this, new EditorDocumentEventArgs(document)));
        }

        return document;
    }

    public void StopTrackingOpenDocument(DocumentId id)
    {
        lock(openDocumentsLock) {
            if (!openDocuments.Remove(id)) {
                throw new ArgumentException("Document is not being tracked");
            }
        }
        if (DocumentClosed is {} handlers) {
            ScheduleTask(() => handlers.Invoke(this, new DocumentEventArgs(id)));
        }
    }

    public EditorDocument GetOpenDocument(DocumentId id)
    {
        if (!openDocuments.TryGetValue(id, out var document)) {
            throw new ArgumentException("Document is not being tracked");
        }
        return document;
    }

    public void UpdateOpenDocument(DocumentId id, SourceText text)
    {
        var document = GetOpenDocument(id);
        document.UpdateText(text);
    }

    public event EventHandler<EditorDocumentEventArgs>? DocumentOpened;
    public event EventHandler<DocumentEventArgs>? DocumentClosed;
}

public class DocumentEventArgs(DocumentId documentId) : EventArgs
{
    public DocumentId DocumentId { get; } = documentId;
}

public class EditorDocumentEventArgs(EditorDocument document) : DocumentEventArgs(document.DocumentId)
{
    public EditorDocument Document { get; } = document;
}
