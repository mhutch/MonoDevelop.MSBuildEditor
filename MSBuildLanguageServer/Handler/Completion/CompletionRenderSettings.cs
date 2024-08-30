// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Roslyn.LanguageServer.Protocol;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler.Completion;

/// <summary>
/// Provides <see cref="ILspCompletionItem.Resolve(CompletionRenderSettings)"/> with information about which properties it should include
/// when creating <see cref="CompletionItem"/> instances for either the completion list for for resolving individual items.
/// </summary>
class CompletionRenderSettings
{
    public CompletionRenderSettings(CompletionClientCapabilities clientCapabilities, bool fullRender)
    {
        ClientCapabilities = clientCapabilities;
        FullRender = fullRender;

        IncludeLabelDetails = ClientCapabilities.LabelDetailsSupport && (fullRender || !clientCapabilities.ResolveSupport.Contains(nameof(CompletionItem.LabelDetails)));
        IncludeItemKind = fullRender || !clientCapabilities.ResolveSupport.Contains(nameof(CompletionItem.Kind));

        bool supportsDeprecatedTag = clientCapabilities.TagSupport.Contains(CompletionItemTag.Deprecated);
        IncludeDeprecatedTag = supportsDeprecatedTag && (fullRender || !ClientCapabilities.ResolveSupport.Contains(nameof(CompletionItem.Tags)));
#pragma warning disable CS0618 // Type or member is obsolete
        IncludeDeprecatedProperty = !supportsDeprecatedTag && ClientCapabilities.DeprecatedSupport && (fullRender || !clientCapabilities.ResolveSupport.Contains(nameof(CompletionItem.Deprecated)));
#pragma warning restore CS0618 // Type or member is obsolete
        IncludeTextEdit = (fullRender || !clientCapabilities.ResolveSupport.Contains(nameof(CompletionItem.TextEdit)));
        IncludeInsertText = (fullRender || !clientCapabilities.ResolveSupport.Contains(nameof(CompletionItem.InsertText)));
        IncludeInsertTextFormat = (fullRender || !clientCapabilities.ResolveSupport.Contains(nameof(CompletionItem.InsertTextFormat)));
    }

    public CompletionClientCapabilities ClientCapabilities { get; }

    public bool FullRender { get; }

    public bool IncludeDocumentation => FullRender;

    public bool IncludeItemKind { get; }

    public bool IncludeLabelDetails { get; }

    public bool IncludeTextEdit { get; }

    public bool IncludeInsertText { get; }
    public bool IncludeInsertTextFormat { get; }

    public bool SupportsInsertReplaceEdit => ClientCapabilities.InsertReplaceSupport;

    /// <summary>
    /// Whether the client supports treating <see cref="CompletionItem.InsertText"/> as a snippet
    /// when <see cref="CompletionItem.InsertTextFormat"/> is set to <see cref="InsertTextFormat.Snippet"/>.
    /// </summary>
    public bool SupportSnippetFormat => ClientCapabilities.SnippetSupport;

    public bool IncludeDeprecatedProperty { get; }

    public bool IncludeDeprecatedTag { get; }

    public bool IncludeDeprecatedPropertyOrTag => IncludeDeprecatedProperty || IncludeDeprecatedTag;

    static readonly CompletionItemTag[] DeprecatedTag = [CompletionItemTag.Deprecated];

    /// <summary>
    /// Marks an item as deprecated using whichever method the client supports.
    /// </summary>
    public void SetDeprecated(CompletionItem item)
    {
        if(IncludeDeprecatedTag)
        {
            if(item.Tags is not null)
            {
                throw new ArgumentException("LSP protocol only defines one tag");
            }
            item.Tags = DeprecatedTag;
        } else if(IncludeDeprecatedProperty)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            item.Deprecated = true;
#pragma warning restore CS0618 // Type or member is obsolete
        }
    }
}
