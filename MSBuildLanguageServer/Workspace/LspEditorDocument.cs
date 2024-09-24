// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Workspace;

class LspEditorDocument
{
    EditorDocumentState state;

    public EditorDocumentState CurrentState => state;

    public DocumentId Id => state.Id;
    public string FilePath => state.FilePath;
    public TextAndVersion Text => state.Text;

    public LspEditorDocument(DocumentId id, string filePath, SourceText initialText)
    {
        state = new EditorDocumentState(id, filePath, TextAndVersion.Create(initialText, VersionStamp.Default));
    }

    internal void UpdateText (SourceText text)
    {
        EditorDocumentState oldState = state;
        state = new EditorDocumentState (
            oldState.Id,
            oldState.FilePath,
            TextAndVersion.Create(text, oldState.Text?.Version.GetNewerVersion() ?? VersionStamp.Default)
        );
    }
}
