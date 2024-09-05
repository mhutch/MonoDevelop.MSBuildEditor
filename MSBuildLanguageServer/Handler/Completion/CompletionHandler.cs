// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Composition;

using Microsoft.CodeAnalysis.LanguageServer;
using Microsoft.CodeAnalysis.LanguageServer.Handler;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.Extensions.Logging;

using MonoDevelop.MSBuild.Editor.Completion;
using MonoDevelop.MSBuild.Editor.LanguageServer.Handler.Completion;
using MonoDevelop.MSBuild.Editor.LanguageServer.Handler.Completion.CompletionItems;
using MonoDevelop.MSBuild.Editor.LanguageServer.Parser;
using MonoDevelop.MSBuild.Editor.LanguageServer.Workspace;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Typesystem;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Editor.Completion;
using MonoDevelop.Xml.Parser;

using Roslyn.LanguageServer.Protocol;

using static MonoDevelop.MSBuild.Language.ExpressionCompletion;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler;

[ExportCSharpVisualBasicStatelessLspService(typeof(CompletionHandler)), Shared]
[Method(Methods.TextDocumentCompletionName)]
[method: ImportingConstructor]
sealed class CompletionHandler([Import(AllowDefault = true)] IMSBuildFileSystem fileSystem)
    : ILspServiceDocumentRequestHandler<CompletionParams, SumType<CompletionItem[], CompletionList>?>
{
    readonly IMSBuildFileSystem fileSystem = fileSystem ?? DefaultMSBuildFileSystem.Instance;

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
        var msbuildReason = ExpressionTriggerReason.Invocation;

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

        // TODO : replace this with collection of schemas
        async Task<MSBuildRootDocument> GetRootDocument()
        {
            if(GetMSBuildParseResult(msbuildParserService, document.CurrentState, cancellationToken) is { } t)
            {
                return (await t).MSBuildDocument;
            } else
            {
                return MSBuildRootDocument.Empty;
            }
        }

        var functionTypeProvider = context.GetRequiredService<FunctionTypeProviderService>().FunctionTypeProvider;

        var msbuildTrigger = MSBuildCompletionTrigger.TryCreate(spine, textSource, msbuildReason, offset, typedCharacter, extLogger, functionTypeProvider, null, cancellationToken);
        if(msbuildTrigger is not null)
        {
            MSBuildRootDocument doc = await GetRootDocument();
            var items = await GetExpressionCompletionList(context, doc, msbuildTrigger, extLogger, sourceText, functionTypeProvider, fileSystem, cancellationToken).ConfigureAwait(false);
            return await CompletionRenderer.RenderCompletionItems(context, request.TextDocument, items, cancellationToken).ConfigureAwait(false);
        }

        (XmlCompletionTrigger kind, int spanStart, int spanLength)? xmlTrigger = null;
        xmlTrigger = XmlCompletionTriggering.GetTriggerAndSpan(spine, xmlReason, typedCharacter, textSource, cancellationToken: cancellationToken);
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

            MSBuildRootDocument doc = await GetRootDocument();

            var clientCapabilities = context.GetRequiredClientCapabilities();
            var clientInfo = context.GetRequiredService<IInitializeManager>().TryGetInitializeParams()?.ClientInfo;

            var rr = MSBuildResolver.Resolve(spine.Clone(), textSource, MSBuildRootDocument.Empty, null, extLogger, cancellationToken);
            var docsProvider = MSBuildCompletionDocsProvider.Create(extLogger, clientCapabilities, clientInfo, doc, sourceText, rr);
            var xmlCompletionContext = new MSBuildXmlCompletionContext(spine, xmlTrigger.Value.kind, textSource, nodePath, position.Line, rr, doc, docsProvider, sourceText);
            var dataSource = new MSBuildXmlCompletionDataSource();
            return await GetXmlCompletionListAsync(context, dataSource, xmlCompletionContext, request.TextDocument, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    async static Task<CompletionList?> GetXmlCompletionListAsync(RequestContext context, MSBuildXmlCompletionDataSource dataSource, MSBuildXmlCompletionContext xmlCompletionContext, TextDocumentIdentifier textDocument, CancellationToken cancellationToken)
    {
        var completionTasks = dataSource.GetCompletionTasks(xmlCompletionContext, cancellationToken);

        var subLists = await Task.WhenAll(completionTasks).ConfigureAwait(false);

        var allItems = subLists.SelectMany(s => s is null ? [] : s);

        return await CompletionRenderer.RenderCompletionItems(context, textDocument, allItems, cancellationToken).ConfigureAwait(false);
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

    async static Task<List<ILspCompletionItem>?> GetExpressionCompletionList(
        RequestContext context, MSBuildRootDocument doc, MSBuildCompletionTrigger trigger,
        ILogger logger, SourceText sourceText, IFunctionTypeProvider functionTypeProvider, IMSBuildFileSystem fileSystem, CancellationToken token)
    {
        var rr = trigger.ResolveResult;

        var clientCapabilities = context.GetRequiredClientCapabilities();
        var clientInfo = context.GetRequiredService<IInitializeManager>().TryGetInitializeParams()?.ClientInfo;
        var docsProvider = MSBuildCompletionDocsProvider.Create(logger, clientCapabilities, clientInfo, doc, sourceText, rr);

        var valueSymbol = rr.GetElementOrAttributeValueInfo(doc);
        if(valueSymbol is null || valueSymbol.ValueKind == MSBuildValueKind.Nothing)
        {
            return null;
        }

        var kind = valueSymbol.ValueKind;

        if(!ValidateListPermitted(trigger.ListKind, kind))
        {
            return null;
        }

        bool allowExpressions = kind.AllowsExpressions();

        kind = kind.WithoutModifiers();

        if(kind == MSBuildValueKind.Data || kind == MSBuildValueKind.Nothing)
        {
            return null;
        }

        // FIXME: This is a temporary hack so we have completion for imported XSD schemas with missing type info.
        // It is not needed for inferred schemas, as they have already performed the inference.
        if(kind == MSBuildValueKind.Unknown)
        {
            kind = MSBuildInferredSchema.InferValueKindFromName(valueSymbol);
        }

        bool isValue = trigger.TriggerState == TriggerState.Value;

        var items = new List<ILspCompletionItem>();

        if(trigger.ComparandVariables != null && isValue)
        {
            foreach(var ci in GetComparandCompletions(doc, fileSystem, trigger.ComparandVariables, logger))
            {
                items.Add(new MSBuildCompletionItem(ci, XmlCommitKind.AttributeValue, docsProvider));
            }
        }

        if(isValue)
        {
            switch(kind)
            {
            /*
            case MSBuildValueKind.NuGetID:
                if(trigger.Expression is ExpressionText t)
                {
                    var packageType = valueSymbol.CustomType?.Values[0].Name;
                    var packageNameItems = await GetPackageNameCompletions(context, t.Value, packageType, token);
                    if(packageNameItems != null)
                    {
                        items.AddRange(packageNameItems);
                    }
                }
                break;
            case MSBuildValueKind.NuGetVersion:
            {
                var packageVersionItems = await GetPackageVersionCompletions(context, token);
                if(packageVersionItems != null)
                {
                    items.AddRange(packageVersionItems);
                }
                break;
            }*/
            case MSBuildValueKind.Sdk:
            case MSBuildValueKind.SdkWithVersion:
            {
                items.AddRange(SdkCompletion.GetSdkCompletions(doc, logger, token).Select(s => new MSBuildSdkCompletionItem(s, docsProvider)));
                break;
            }
            case MSBuildValueKind.Lcid:
                items.AddRange(CultureHelper.GetKnownCultures().Select(c => new MSBuildLcidCompletionItem(c, docsProvider)));
                break;
            case MSBuildValueKind.Culture:
                items.AddRange(CultureHelper.GetKnownCultures().Select(c => new MSBuildCultureCompletionItem(c, docsProvider)));
                break;
            }

            if(kind == MSBuildValueKind.Guid || valueSymbol.CustomType is CustomTypeInfo { BaseKind: MSBuildValueKind.Guid, AllowUnknownValues: true })
            {
                items.Add(new MSBuildNewGuidCompletionItem());
            }
        }

        //TODO: better metadata support
        if(valueSymbol.CustomType != null && valueSymbol.CustomType.Values.Count > 0 && isValue)
        {
            bool addDescriptionHint = CompletionHelpers.ShouldAddHintForCompletions(valueSymbol);
            foreach(var value in valueSymbol.CustomType.Values)
            {
                items.Add(new MSBuildCompletionItem(value, XmlCommitKind.AttributeValue, docsProvider, addDescriptionHint: addDescriptionHint));
            }

        } else
        {
            //FIXME: can we avoid awaiting this unless we actually need to resolve a function? need to propagate async downwards
            await functionTypeProvider.EnsureInitialized(token);
            if(GetCompletionInfos(rr, trigger.TriggerState, valueSymbol, trigger.Expression, trigger.SpanLength, doc, functionTypeProvider, fileSystem, logger, kindIfUnknown: kind) is IEnumerable<ISymbol> completionInfos)
            {
                bool addDescriptionHint = CompletionHelpers.ShouldAddHintForCompletions(valueSymbol);
                foreach(var ci in completionInfos)
                {
                    items.Add(new MSBuildCompletionItem(ci, XmlCommitKind.AttributeValue, docsProvider, addDescriptionHint: addDescriptionHint));
                }
            }
        }

        if(allowExpressions && isValue || trigger.TriggerState == TriggerState.BareFunctionArgumentValue)
        {
            items.Add(new MSBuildReferenceExpressionCompletionItem("$(", "Property reference", MSBuildCompletionItemKind.PropertySyntax));
        }

        if(allowExpressions && isValue)
        {
            items.Add(new MSBuildReferenceExpressionCompletionItem("@(", "Item reference", MSBuildCompletionItemKind.ItemSyntax));
            if(CompletionHelpers.IsMetadataAllowed(trigger.Expression, rr))
            {
                items.Add(new MSBuildReferenceExpressionCompletionItem("%(", "Metadata reference", MSBuildCompletionItemKind.MetadataSyntax));
            }
        }

        return items;

        //throw new NotImplementedException();
    }

    /*
    CompletionItem CreateNuGetCompletionItem(MSBuildCompletionDocumentationProvider documentationProvider, Tuple<string, FeedKind> info, XmlCompletionItemKind xmlCompletionItemKind)
    {
        var kindImage = provider.DisplayElementFactory.GetImageElement(info.Item2);
        var item = new CompletionItem(info.Item1, this, kindImage);
        item.AddKind(xmlCompletionItemKind);
        documentationProvider.AttachDocumentation(item, info);
        return item;
    }

    CompletionItem CreateOrderedNuGetCompletionItem(MSBuildCompletionDocumentationProvider documentationProvider, Tuple<string, FeedKind> info, XmlCompletionItemKind xmlCompletionItemKind, int index)
    {
        var kindImage = provider.DisplayElementFactory.GetImageElement(info.Item2);
        string displayText = info.Item1;
        var item = new CompletionItem(displayText, this, kindImage, ImmutableArray<CompletionFilter>.Empty, string.Empty, displayText, $"_{index:D5}", displayText, ImmutableArray<ImageElement>.Empty);
        item.AddKind(xmlCompletionItemKind);
        documentationProvider.AttachDocumentation(item, info);
        return item;
    }
    */
}