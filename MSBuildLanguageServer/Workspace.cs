// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace MonoDevelop.MSBuild.Editor.LanguageServer;

/// <summary>
/// Tracks files of interest to the editor, including files that are open
/// and files that are imported by open files.
/// </summary>
public abstract partial class Workspace
{
    // Maps of files are keyed on document ID, which is a unique identifier for a document in the workspace.
    // The document ID is generated on demand and is stable for the lifetime of the document,
    // which reduces the need to propagate document renames through the system.
    // The ID is not derived from the path of the document, but is internally a GUID. Although this
    // means it is not deterministic, it also means we can track documents that don't have a name.
    // We do not currently check for GUID collisions but could do so fairly easily if needed.
    // TODO: need to ensure that the path is absolute and fully normalized, and take a account of case sensitivity
    readonly object documentIdLock = new ();
    readonly Dictionary<string,DocumentId> filePathToDocumentId = new();

    public bool TryGetDocumentId(string normalizedAbsoluteFilePath, [NotNullWhen(true)] out DocumentId? id)
        => filePathToDocumentId.TryGetValue(normalizedAbsoluteFilePath, out id);

    protected DocumentId GetOrCreateDocumentId(string normalizedAbsoluteFilePath)
    {
        if (filePathToDocumentId.TryGetValue(normalizedAbsoluteFilePath, out var id))
        {
            return id;
        }

        lock (documentIdLock)
        {
            id = DocumentId.CreateNewId();
            if (!filePathToDocumentId.TryAdd(normalizedAbsoluteFilePath, id)) {
                id = filePathToDocumentId[normalizedAbsoluteFilePath];
            }
        }

        return id;
    }
}
