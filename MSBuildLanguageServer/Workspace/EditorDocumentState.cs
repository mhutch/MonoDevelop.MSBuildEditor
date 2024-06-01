// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Workspace;

class EditorDocumentState (DocumentId id, string filePath, TextAndVersion text)
{
    public DocumentId Id => id;
    public string FilePath => filePath;
    public TextAndVersion Text => text ?? throw new InvalidOperationException("Text not yet loaded");
}
