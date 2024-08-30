// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.LanguageServer.Handler;
using Microsoft.CodeAnalysis.PooledObjects;

using MonoDevelop.MSBuild.Editor.LanguageServer.Services;

using Roslyn.LanguageServer.Protocol;

using CompletionResolveData = Microsoft.CodeAnalysis.LanguageServer.Handler.Completion.CompletionResolveData;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler.Completion;

static class CompletionRenderer
{
    public static async Task<CompletionList?> RenderCompletionItems(RequestContext context, TextDocumentIdentifier textDocument, IEnumerable<ILspCompletionItem> items, CancellationToken cancellationToken)
    {
        var clientCapabilities = context.GetRequiredClientCapabilities();

        var completionCapabilities = CompletionClientCapabilities.Create(clientCapabilities);
        var renderSettings = new CompletionRenderSettings(completionCapabilities, false);

        var completionListCache = context.GetRequiredService<CompletionListCache>();

        var rawItems = new List<ILspCompletionItem>();
        var resultId = completionListCache.UpdateCache(new CompletionListCacheEntry(rawItems));
        var resolveData = new CompletionResolveData(resultId, textDocument);

        using var _ = ArrayBuilder<CompletionItem>.GetInstance(out var renderedBuilder);

        bool supportsDataDefault = completionCapabilities.SupportedItemDefaults.Contains(nameof(CompletionItem.Data));

        // could consider parallelizing this
        // however, the common case should be that Render returns a completed ValueTask, so it's faster than it looks
        foreach(var item in items)
        {
            rawItems.Add(item);
            var renderedItem = await item.Render(renderSettings, cancellationToken).ConfigureAwait(false);
            if(!supportsDataDefault)
            {
                renderedItem.Data = resolveData;
            }
            renderedBuilder.Add(renderedItem);
        }

        var completionList = new CompletionList {
            Items = renderedBuilder.ToArray()
        };

        if(supportsDataDefault)
        {
            completionList.ItemDefaults = new CompletionListItemDefaults() { Data = resolveData };
        }

        return completionList;
    }
}
