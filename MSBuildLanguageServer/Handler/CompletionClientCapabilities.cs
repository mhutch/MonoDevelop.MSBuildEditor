// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Roslyn.LanguageServer.Protocol;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler;

/// <summary>
/// Helper that makes reading client completion capabilities simpler and more performant.
/// </summary>
class CompletionClientCapabilities
{
    public static CompletionClientCapabilities Create(ClientCapabilities clientCapabilities) => new(clientCapabilities);

    CompletionClientCapabilities (ClientCapabilities clientCapabilities)
    {
        var completionSetting = clientCapabilities.TextDocument?.Completion;

        ContextSupport = completionSetting?.ContextSupport ?? false;

        SupportedItemDefaults = completionSetting?.CompletionListSetting?.ItemDefaults?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

        SupportedItemKinds = completionSetting?.CompletionItemKind?.ValueSet?.ToHashSet() ?? [];

        DefaultInsertTextMode = completionSetting?.InsertTextMode;

        LabelDetailsSupport = completionSetting?.CompletionItem?.LabelDetailsSupport ?? false;

        CommitCharactersSupport = completionSetting?.CompletionItem?.CommitCharactersSupport ?? false;

#pragma warning disable CS0618 // Type or member is obsolete
        DeprecatedSupport = completionSetting?.CompletionItem?.DeprecatedSupport ?? false;
#pragma warning restore CS0618 // Type or member is obsolete

        DocumentationFormat = completionSetting?.CompletionItem?.DocumentationFormat?.ToHashSet() ?? [];

        InsertReplaceSupport = completionSetting?.CompletionItem?.InsertReplaceSupport ?? false;

        InsertTextModeSupport = completionSetting?.CompletionItem?.InsertTextModeSupport?.ValueSet.ToHashSet() ?? [];

        PreselectSupport = completionSetting?.CompletionItem?.PreselectSupport ?? false;

        ResolveSupport = completionSetting?.CompletionItem?.ResolveSupport?.Properties.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

        SnippetSupport = completionSetting?.CompletionItem?.SnippetSupport ?? false;

        TagSupport = completionSetting?.CompletionItem?.TagSupport?.ValueSet.ToHashSet() ?? [];
    }

    /// <summary>
    /// Whether the client supports providing additional context via the <see cref="CompletionParams.Context"/> property
    /// </summary>
    public bool ContextSupport { get; }

    /// <summary>
    /// The property names that the client supports on the <see cref="CompletionList.ItemDefaults"/> collection
    /// </summary>
    public HashSet<string> SupportedItemDefaults { get; }

    /// <summary>
    /// The <see cref="CompletionItemKind"/> values supported by the client
    /// </summary>
    public HashSet<CompletionItemKind> SupportedItemKinds { get; }

    /// <summary>
    /// The client's default insertion behavior for items that do not specify a <see cref="CompletionItem.InsertTextMode"/> value
    /// </summary>
    public InsertTextMode? DefaultInsertTextMode { get; }

    /// <summary>
    /// Whether the client supports the <see cref="CompletionItem.LabelDetails"/> property
    /// </summary>
    public bool LabelDetailsSupport { get; }

    /// <summary>
    /// Whether the client supports the <see cref="CompletionItem.CommitCharacters"/> property
    /// </summary>
    public bool CommitCharactersSupport { get; }

    /// <summary>
    /// Whether the client supports the deprecated <see cref="CompletionItem.Deprecated"/> property
    /// </summary>
    public bool DeprecatedSupport { get; }

    /// <summary>
    /// Which formats the client supports for documentation
    /// </summary>
    public HashSet<MarkupKind> DocumentationFormat { get; }

    /// <summary>
    /// Whether the client supports <see cref="InsertReplaceEdit"/> values on the <see cref="CompletionItem.TextEdit"/> property
    /// </summary>
    public bool InsertReplaceSupport { get; }

    /// <summary>
    /// Whether the client supports the <see cref="CompletionItem.InsertTextMode"/> property
    /// </summary>
    public HashSet<InsertTextMode> InsertTextModeSupport { get; }

    /// <summary>
    /// Whether the client supports the <see cref="CompletionItem.Preselect"/> property
    /// </summary>
    public bool PreselectSupport { get; }

    /// <summary>
    /// Which properties the client supports resolving 
    /// </summary>
    public HashSet<string> ResolveSupport { get; }

    /// <summary>
    /// Whether the client supports treating <see cref="CompletionItem.InsertText"/> as a snippet
    /// when <see cref="CompletionItem.InsertTextFormat"/> is set to <see cref="InsertTextFormat.Snippet"/>.
    /// </summary>
    public bool SnippetSupport { get; }

    /// <summary>
    /// The <see cref="CompletionItemTag"/> values supported by the client
    /// </summary>
    public HashSet<CompletionItemTag> TagSupport { get; }
}
