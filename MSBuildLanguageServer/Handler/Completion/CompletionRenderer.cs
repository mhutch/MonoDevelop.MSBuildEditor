// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.LanguageServer.Handler;
using Microsoft.CodeAnalysis.PooledObjects;

using MonoDevelop.MSBuild.Editor.LanguageServer.Services;

using Roslyn.LanguageServer.Protocol;
using LSP = Roslyn.LanguageServer.Protocol;

using CompletionResolveData = Microsoft.CodeAnalysis.LanguageServer.Handler.Completion.CompletionResolveData;
using Microsoft.CodeAnalysis.Text;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler.Completion;

static class CompletionRenderer
{
    public static async Task<CompletionList?> RenderCompletionItems(
        RequestContext context,
        TextDocumentIdentifier textDocument,
        IEnumerable<ILspCompletionItem> items,
        SourceText sourceText,
        LSP.Range editRange,
        CancellationToken cancellationToken)
    {
        var clientCapabilities = context.GetRequiredClientCapabilities();

        var completionCapabilities = CompletionClientCapabilities.Create(clientCapabilities);
        var renderSettings = new CompletionRenderSettings(completionCapabilities, false);

        var completionListCache = context.GetRequiredService<CompletionListCache>();

        var rawItems = new List<ILspCompletionItem>();
        var renderContext = new CompletionRenderContext(editRange, sourceText);
        var resultId = completionListCache.UpdateCache(new CompletionListCacheEntry(rawItems, renderContext));
        var resolveData = new CompletionResolveData(resultId, textDocument);

        using var _ = ArrayBuilder<CompletionItem>.GetInstance(out var renderedBuilder);

        // If the client doesn't support EditRange, and the item doesn't define a TextEdit, we must add a computed one.
        // If the client doesn't support resolving the TextEdit in the resolve handler, then we must do this upfront.
        bool includeEditRangeTextEdit = !renderSettings.SupportsEditRange && renderSettings.IncludeTextEdit;

        // could consider parallelizing this
        // however, the common case should be that Render returns a completed ValueTask, so it's faster than it looks
        foreach(var item in items)
        {
            rawItems.Add(item);

            var renderedItem = await item.Render(renderSettings, renderContext, cancellationToken).ConfigureAwait(false);

            if(!renderSettings.SupportsDataDefault)
            {
                renderedItem.Data = resolveData;
            }

            if(includeEditRangeTextEdit)
            {
                renderedItem.AddEditRangeTextEdit(renderSettings, renderContext);
            }

            renderedBuilder.Add(renderedItem);
        }

        var completionList = new CompletionList {
            Items = renderedBuilder.ToArray()
        };


        if(renderSettings.SupportsDataDefault)
        {
            (completionList.ItemDefaults ??= new()).Data = resolveData;
        }


        if(renderSettings.SupportsEditRange)
        {
            (completionList.ItemDefaults ??= new()).EditRange = editRange;
        }

        return completionList;
    }

    /// <summary>
    /// If the item doesn't define a TextEdit, then compute one based on the EditRange in the <paramref name="settings"/>
    /// </summary>
    public static void AddEditRangeTextEdit(this CompletionItem item, CompletionRenderSettings settings, CompletionRenderContext ctx)
    {
        if (item.TextEdit is null)
        {
            item.TextEdit = new TextEdit {
                NewText = item.TextEditText ?? item.InsertText ?? item.Label,
                Range = ctx.EditRange
            };
        }
    }
}
