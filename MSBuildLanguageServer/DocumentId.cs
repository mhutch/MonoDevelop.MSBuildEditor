// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MonoDevelop.MSBuild.Editor.LanguageServer;

/// Unique persistent identifier for a document in a workspace.
/// Dot not change when the document is renamed.
/// The document may not be saved to disk, so may not have a file path.
/// </summary>
public class DocumentId : IEquatable<DocumentId>
{
    readonly Guid guid;

    private DocumentId(Guid guid)
    {
        this.guid = guid;
    }

    public static DocumentId CreateNewId() => new (Guid.NewGuid());

    public bool Equals(DocumentId? other) => other is not null && guid == other.guid;

    public override bool Equals(object? obj) => obj is DocumentId id && guid.Equals(id.guid);

    public override int GetHashCode() => guid.GetHashCode();
}