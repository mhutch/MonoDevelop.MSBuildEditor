// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace MonoDevelop.MSBuild.Editor.LanguageServer;

/// <summary>
/// Represents a document that is open in the editor.
/// </summary>
public class EditorDocument
{
    readonly object lockObj = new();

    VersionedSourceText? sourceText;

    internal EditorDocument(EditorWorkspace workspace, DocumentId documentId, SourceText? sourceText)
    {
        Workspace = workspace;
        DocumentId = documentId;
        if (sourceText is not null) {
            this.sourceText = new VersionedSourceText(sourceText, 0);
        }
    }

    public EditorWorkspace Workspace { get; }
    public DocumentId DocumentId { get; }
    public SourceText SourceText => sourceText?.Text ?? throw new InvalidOperationException("Document has not yet been initialized");
    public int Version => sourceText?.Version ?? -1;

    internal void UpdateText(SourceText sourceText)
    {
        VersionedSourceText? newText = null, oldText = null;
        lock(lockObj) {
            oldText = this.sourceText;
            this.sourceText = newText = new VersionedSourceText(sourceText, oldText?.Version + 1 ?? 0);
        }

        if (SourceTextUpdated is {} evt) {
            evt?.Invoke(this, new DocumentSourceTextUpdated(DocumentId, oldText, newText));
        }
    }

    public event EventHandler<DocumentSourceTextUpdated>? SourceTextUpdated;
}

public record class VersionedSourceText(SourceText Text, int Version);

public class DocumentSourceTextUpdated : DocumentEventArgs
{
    readonly VersionedSourceText? oldText;
    readonly VersionedSourceText newText;
    public DocumentSourceTextUpdated(DocumentId documentId, VersionedSourceText? oldText, VersionedSourceText newText)
        : base(documentId)
    {
        this.oldText = oldText;
        this.newText = newText;
    }
    public SourceText? OldText => oldText?.Text;
    public int OldVersion => oldText?.Version ?? -1;
    public SourceText NewText => newText.Text;
    public int NewVersion => newText.Version;
}