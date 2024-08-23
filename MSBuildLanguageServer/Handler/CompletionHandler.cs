// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Composition;

using Microsoft.CodeAnalysis.LanguageServer;
using Microsoft.CodeAnalysis.LanguageServer.Handler;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CommonLanguageServerProtocol.Framework;
using CompletionResolveData = Microsoft.CodeAnalysis.LanguageServer.Handler.Completion.CompletionResolveData;

using MonoDevelop.MSBuild.Editor.LanguageServer.Parser;
using MonoDevelop.MSBuild.Editor.LanguageServer.Workspace;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Editor.Completion;
using MonoDevelop.Xml.Parser;

using Roslyn.LanguageServer.Protocol;
using MonoDevelop.MSBuild.Editor.LanguageServer.Services;
using System.Text.Json;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler;

[ExportCSharpVisualBasicStatelessLspService(typeof(CompletionHandler)), Shared]
[Method(Methods.TextDocumentCompletionName)]
sealed class CompletionHandler : ILspServiceDocumentRequestHandler<CompletionParams, SumType<CompletionItem[], CompletionList>?>
{
    public bool MutatesSolutionState => false;

    public bool RequiresLSPSolution => true;

    public TextDocumentIdentifier GetTextDocumentIdentifier(CompletionParams request) => request.TextDocument;

    readonly XmlRootState stateMachine = new();

    public async Task<SumType<CompletionItem[], CompletionList>?> HandleRequestAsync(CompletionParams request, RequestContext context, CancellationToken cancellationToken)
    {
        // HELP: CompletionTriggerKind maps really poorly
        //
        // VS has CompletionTriggerReason.Insertion|Backspace|Invoke|InvokeAndCommitIfUnique and a few others
        // and XmlEditor maps this to its internal type XmlTriggerReason.TypedChar|Backspace|Invocation.
        //
        // However, in LSP the distinction is between
        //  * CompletionTriggerKind.TypedCharacter, which means a character we *explicitly* marked as a trigger char was typed
        //  * CompletionTriggerKind.Invoked, which means completion was manually invoked or any character was typed
        //
        // CompletionTriggerKind.TypedCharacter isn't useful to us, and so we only handle Invoked. However, this
        // maps to both XmlTriggerReason.TypedChar and XmlTriggerReason.Invocation, which both handle subtly
        // different behaviors for implicit vs explicit completion invocation. For now, we will just map it
        // to Invocation and review the details later.

        switch(request.Context?.TriggerKind ?? CompletionTriggerKind.Invoked)
        {
        // user typed [a-zA-Z] or explicitly triggered completion
        case CompletionTriggerKind.Invoked:
        // user typed a char in ServerCapabilities.CompletionOptions.TriggerCharacters
        case CompletionTriggerKind.TriggerCharacter:
        // we marked a completion list as incomplete and user typed another char
        case CompletionTriggerKind.TriggerForIncompleteCompletions:
            break;
        // unknown, ignore
        default:
            return null;
        }

        var xmlReason = XmlTriggerReason.Invocation;
        var msbuildReason = ExpressionCompletion.ExpressionTriggerReason.Invocation;

        var document = context.GetRequiredDocument();
        var sourceText = document.Text.Text;
        var textSource = sourceText.GetTextSource();
        var position = ProtocolConversions.PositionToLinePosition(request.Position);
        int offset = position.ToOffset(sourceText);
        var typedCharacter = offset < 0 ? '\0' : sourceText[offset - 1];

        var msbuildParserService = context.GetRequiredService<LspMSBuildParserService>();
        var xmlParserService = context.GetRequiredService<LspXmlParserService>();
        var logger = context.GetRequiredService<ILspLogger>();
        var extLogger = logger.ToILogger();

        // get a spine, reusing anything we can from the last completed parse
        // TODO: port the caching spine parser?
        xmlParserService.TryGetCompletedParseResult(document.CurrentState, out var lastXmlParse);
        var spine = LspXmlParserService.GetSpineParser(stateMachine, lastXmlParse, position, sourceText, cancellationToken);

        (XmlCompletionTrigger kind, int spanStart, int spanLength)? xmlTrigger = null;
        xmlTrigger = XmlCompletionTriggering.GetTriggerAndSpan(spine, xmlReason, typedCharacter, textSource);
        if(xmlTrigger.Value.kind != XmlCompletionTrigger.None)
        {
            // ignore the case where the method returns false when the deepest node's name cannot be completed, we will try to provide completion anyways
            spine.TryGetNodePath(textSource, out var nodePath, cancellationToken: cancellationToken);

            // if we're completing an existing element, remove it from the path
            // so we don't get completions for its children instead
            if((xmlTrigger.Value.kind == XmlCompletionTrigger.ElementName || xmlTrigger.Value.kind == XmlCompletionTrigger.Tag) && nodePath?.Count > 0)
            {
                if(nodePath[nodePath.Count - 1] is XElement leaf && leaf.Name.Length == xmlTrigger.Value.spanLength)
                {
                    nodePath.RemoveAt(nodePath.Count - 1);
                }
            }

            // TODO : replace this with collection of schemas
            MSBuildRootDocument doc;
            if (GetMSBuildParseResult(msbuildParserService, document.CurrentState, cancellationToken) is { } t)
            {
                doc = (await t).MSBuildDocument;
            } else
            {
                doc = MSBuildRootDocument.Empty;
            }

            var clientCapabilities = context.GetRequiredClientCapabilities();
            var completionCapabilities = CompletionClientCapabilities.Create(clientCapabilities);
            var clientInfo = context.GetRequiredService<IInitializeManager>().TryGetInitializeParams()?.ClientInfo;

            var rr = MSBuildResolver.Resolve(spine.Clone(), textSource, MSBuildRootDocument.Empty, null, extLogger, cancellationToken);
            var renderer = new DisplayElementRenderer(extLogger, clientCapabilities, clientInfo, clientCapabilities.TextDocument?.Completion?.CompletionItem?.DocumentationFormat);
            var xmlCompletionContext = new MSBuildXmlCompletionContext(spine, xmlTrigger.Value.kind, textSource, nodePath, position.Line, rr, doc, renderer, sourceText);
            var dataSource = new MSBuildXmlCompletionDataSource();
            return await GetXmlCompletionListAsync(context, dataSource, xmlCompletionContext, completionCapabilities, request.TextDocument, cancellationToken).ConfigureAwait(false);
        }

        var msbuildTrigger = MSBuildCompletionTrigger.TryCreate(spine, textSource, msbuildReason, offset, typedCharacter, extLogger, null, cancellationToken);

        if(msbuildTrigger is not null)
        {
            return await GetExpressionCompletionList(context, msbuildTrigger, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    async static Task<CompletionList?> GetExpressionCompletionList(RequestContext context, MSBuildCompletionTrigger msbuildTrigger, CancellationToken cancellationToken)
    {
        return null;
        //throw new NotImplementedException();
    }

    async static Task<CompletionList?> GetXmlCompletionListAsync(RequestContext context, MSBuildXmlCompletionDataSource dataSource, MSBuildXmlCompletionContext xmlCompletionContext, CompletionClientCapabilities completionCapabilities, TextDocumentIdentifier textDocument, CancellationToken cancellationToken)
    {
        var completionTasks = dataSource.GetCompletionTasks(xmlCompletionContext, cancellationToken);

        var subLists = await Task.WhenAll(completionTasks).ConfigureAwait(false);

        var renderSettings = new CompletionRenderSettings(completionCapabilities, false);

        var completionListCache = context.GetRequiredService<CompletionListCache>();

        var rawItems = new List<ILspCompletionItem>();
        var resultId = completionListCache.UpdateCache(new CompletionListCacheEntry(rawItems));
        var resolveData = new CompletionResolveData(resultId, textDocument);

        using var _ = ArrayBuilder<CompletionItem>.GetInstance(out var renderedBuilder);

        bool supportsDataDefault = completionCapabilities.SupportedItemDefaults.Contains(nameof(CompletionItem.Data));

        foreach(var subList in subLists)
        {
            if (subList is null)
            {
                continue;
            }
            foreach (var item in subList)
            {
                rawItems.Add(item);
                var renderedItem = await item.Render(renderSettings, cancellationToken).ConfigureAwait(false);
                if (!supportsDataDefault)
                {
                    renderedItem.Data = resolveData;
                }
                renderedBuilder.Add(renderedItem);
            }
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

    static Task<MSBuildParseResult>? GetMSBuildParseResult(LspMSBuildParserService msbuildParserService, EditorDocumentState documentState, CancellationToken cancellationToken)
    {
        // if we determined we are triggering, now we can look up the current parsed document
        // FIXME: do we need it to be current or will a stale one do? it's really just used for schemas
        if(msbuildParserService.TryGetCompletedParseResult(documentState, out var parseResult))
        {
            return Task.FromResult(parseResult);
        } else if(msbuildParserService.TryGetParseResult(documentState, out Task<MSBuildParseResult>? parseTask, cancellationToken))
        {
            return parseTask;
        } else
        {
            return null;
        }
    }
}

[ExportCSharpVisualBasicStatelessLspService(typeof(CompletionResolveHandler)), Shared]
[Method(Methods.TextDocumentCompletionResolveName)]
sealed class CompletionResolveHandler : ILspServiceRequestHandler<CompletionItem, CompletionItem>
{
    public bool MutatesSolutionState => false;
    public bool RequiresLSPSolution => false;

    public async Task<CompletionItem> HandleRequestAsync(CompletionItem request, RequestContext context, CancellationToken cancellationToken)
    {
        if (request.Data is not JsonElement data)
        {
            throw new InvalidOperationException("Completion item is missing data");
        }
        var resolveData = JsonSerializer.Deserialize<CompletionResolveData>(data, ProtocolConversions.LspJsonSerializerOptions);
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
            if(item.IsMatch(request)) {
                var renderedItem = await item.Render(renderSettings, cancellationToken).ConfigureAwait(false);
                // avoid ambiguity if there are multiple items with the same label and IsMatch didn't distinguish
                if (!string.Equals(renderedItem.SortText, request.SortText) || !string.Equals(renderedItem.FilterText, request.FilterText))
                {
                    continue;
                }
                return renderedItem;
            };
        }

        throw new InvalidOperationException("Did not find completion item in cached list");
    }
}