// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Composition;
using System.Text.Json;

using Microsoft.CodeAnalysis.LanguageServer;
using Microsoft.CodeAnalysis.LanguageServer.Handler;

using MonoDevelop.MSBuild.Editor.LanguageServer.Services;

using Roslyn.LanguageServer.Protocol;

using CompletionResolveData = Microsoft.CodeAnalysis.LanguageServer.Handler.Completion.CompletionResolveData;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler.Completion;

[ExportCSharpVisualBasicStatelessLspService(typeof(CompletionResolveHandler)), Shared]
[Method(Methods.TextDocumentCompletionResolveName)]
sealed class CompletionResolveHandler : ILspServiceRequestHandler<CompletionItem, CompletionItem>
{
    public bool MutatesSolutionState => false;
    public bool RequiresLSPSolution => false;

    public async Task<CompletionItem> HandleRequestAsync(CompletionItem request, RequestContext context, CancellationToken cancellationToken)
    {
        if(request.Data is not JsonElement data)
        {
            throw new InvalidOperationException("Completion item is missing data");
        }
        var resolveData = data.Deserialize<CompletionResolveData>(ProtocolConversions.LspJsonSerializerOptions);
        if(resolveData?.ResultId == null)
        {
            throw new InvalidOperationException("Completion item is missing resultId");
        }

        var completionListCache = context.GetRequiredService<CompletionListCache>();

        var cacheEntry = completionListCache.GetCachedEntry(resolveData.ResultId);
        if(cacheEntry == null)
        {
            throw new InvalidOperationException("Did not find cached completion list");
        }

        var clientCapabilities = context.GetRequiredClientCapabilities();
        var completionCapabilities = CompletionClientCapabilities.Create(clientCapabilities);
        var renderSettings = new CompletionRenderSettings(completionCapabilities, true);

        foreach(var item in cacheEntry.Items)
        {
            if(item.IsMatch(request))
            {
                var renderedItem = await item.Render(renderSettings, cancellationToken).ConfigureAwait(false);
                // avoid ambiguity if there are multiple items with the same label and IsMatch didn't distinguish
                if(!string.Equals(renderedItem.SortText, request.SortText) || !string.Equals(renderedItem.FilterText, request.FilterText))
                {
                    continue;
                }
                return renderedItem;
            };
        }

        throw new InvalidOperationException("Did not find completion item in cached list");
    }
}