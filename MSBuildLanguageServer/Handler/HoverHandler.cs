// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Composition;

using Microsoft.CodeAnalysis.LanguageServer;
using Microsoft.CodeAnalysis.LanguageServer.Handler;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CommonLanguageServerProtocol.Framework;

using MonoDevelop.MSBuild.Editor.LanguageServer.Parser;
using MonoDevelop.MSBuild.Editor.NuGetSearch;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Language.Typesystem;
using MonoDevelop.MSBuild.PackageSearch;
using MonoDevelop.MSBuild.Schema;

using ProjectFileTools.NuGetSearch.Contracts;

using Roslyn.LanguageServer.Protocol;

using Range = Roslyn.LanguageServer.Protocol.Range;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler;

// partly based on MonoDevelop.MSBuild.Editor/QuickInfo/MSBuildQuickInfoSource.cs

[ExportCSharpVisualBasicStatelessLspService(typeof(HoverHandler)), Shared]
[Method(Methods.TextDocumentHoverName)]
internal sealed class HoverHandler : ILspServiceDocumentRequestHandler<TextDocumentPositionParams, Hover?>
{
    public bool MutatesSolutionState => false;

    public bool RequiresLSPSolution => true;

    public TextDocumentIdentifier GetTextDocumentIdentifier(TextDocumentPositionParams request) => request.TextDocument;

    public async Task<Hover?> HandleRequestAsync(TextDocumentPositionParams request, RequestContext context, CancellationToken cancellationToken)
    {
        var msbuildParserService = context.GetRequiredService<LspMSBuildParserService>();
        var xmlParserService = context.GetRequiredService<LspXmlParserService>();
        var logger = context.GetRequiredService<ILspLogger>();
        var extLogger = logger.ToILogger();

        var document = context.GetRequiredDocument();

        if(!msbuildParserService.TryGetParseResult(document.CurrentState, out Task<MSBuildParseResult>? parseTask, cancellationToken))
        {
            return null;
        }

        var result = await parseTask!; // not sure why we need the ! here, TryGetParseResult has NotNullWhen(true)

        if(result?.MSBuildDocument is not MSBuildRootDocument doc)
        {
            return null;
        }

        DisplayElementRenderer CreateRenderer()
        {
            var clientCapabilities = context.GetRequiredClientCapabilities();
            var formats = clientCapabilities.TextDocument?.Hover?.ContentFormat;
            return new DisplayElementRenderer(extLogger, clientCapabilities, formats);
        }

        var sourceText = result.XmlParseResult.Text;
        var position = ProtocolConversions.PositionToLinePosition(request.Position);
        int offset = position.ToOffset (sourceText);

        var spineParser = result.XmlParseResult.GetSpineParser(position, cancellationToken);

        var annotations = MSBuildNavigation.GetAnnotationsAtOffset<NavigationAnnotation>(doc, offset)?.ToList();
        if(annotations != null && annotations.Count > 0)
        {
            // TODO navigation annotations
            var renderer = CreateRenderer();
            return CreateNavigationQuickInfo(renderer, sourceText, annotations);
        }

        var functionTypeProvider = new NullFunctionTypeProvider();

        //FIXME: can we avoid awaiting this unless we actually need to resolve a function? need to propagate async downwards
        await functionTypeProvider.EnsureInitialized(cancellationToken);

        var rr = MSBuildResolver.Resolve(spineParser, result.XmlParseResult.Text.GetTextSource(), result.MSBuildDocument, functionTypeProvider, extLogger, cancellationToken);
        if (rr is null)
        {
            return null;
        }

        if(rr.ReferenceKind == MSBuildReferenceKind.NuGetID)
        {
            var packageSearchManager = context.GetRequiredService<NuGetSearchService>();
            var renderer = CreateRenderer();
            return await CreateNuGetQuickInfo(renderer, packageSearchManager, logger, sourceText, doc, rr, cancellationToken);
        }

        var info = rr.GetResolvedReference(doc, functionTypeProvider);
        if(info is null) {
            return null;
        }

        // don't include the deprecation message, as the validator should have added a warning that will be merged into this tooltip
        var markdown = await CreateRenderer().GetInfoTooltipElement(sourceText, doc, info, rr, false, cancellationToken);

        if(markdown is not null) {
            return CreateHover(rr.ToLspRange(sourceText), markdown);
        }
        return null;
    }

    static Hover CreateHover(Range range, string markdown)
    {
        return new Hover {
            Range = range,
            Contents = new MarkupContent {
                Kind = MarkupKind.Markdown,
                Value = markdown
            }
        };
    }

    Hover CreateNavigationQuickInfo(DisplayElementRenderer renderer, SourceText sourceText, IEnumerable<NavigationAnnotation> annotations)
    {
        var navs = annotations.ToList();
        var first = navs.First();
        var resolvedPathMarkdown = renderer.GetResolvedPathElement(navs);

        return CreateHover(first.Span.ToLspRange (sourceText), resolvedPathMarkdown);
    }

    //FIXME: can we display some kind of "loading" message while it loads?
    async Task<Hover?> CreateNuGetQuickInfo(
        DisplayElementRenderer renderer,
        IPackageSearchManager packageSearchManager, ILspLogger logger,
        SourceText sourceText, MSBuildRootDocument doc, MSBuildResolveResult rr, CancellationToken token)
    {
        IPackageInfo? info = null;
        var packageId = rr.GetNuGetIDReference();

        try
        {
            var frameworkId = doc.GetTargetFrameworkNuGetSearchParameter();

            //FIXME: can we use the correct version here?
            var infos = await packageSearchManager.SearchPackageInfo(packageId, null, frameworkId).ToTask(token);

            //prefer non-local results as they will have more metadata
            info = infos
                .FirstOrDefault(p => p.SourceKind != ProjectFileTools.NuGetSearch.Feeds.FeedKind.Local)
                ?? infos.FirstOrDefault();
        } catch(Exception ex) when(!(ex is OperationCanceledException && token.IsCancellationRequested))
        {
            logger.LogException(ex);
        }

        if (info is not null)
        {
            var markdown = renderer.GetPackageInfoTooltip(packageId, info, info.SourceKind);
            return CreateHover(rr.ToLspRange (sourceText), markdown);
        }

        return null;
    }

    class NullFunctionTypeProvider : IFunctionTypeProvider
    {
        public Task EnsureInitialized(CancellationToken token) => Task.CompletedTask;
        public ClassInfo? GetClassInfo(string name) => null;
        public IEnumerable<ClassInfo> GetClassNameCompletions() => [];
        public ISymbol? GetEnumInfo(string reference) => null;
        public FunctionInfo? GetItemFunctionInfo(string name) => null;
        public IEnumerable<FunctionInfo> GetItemFunctionNameCompletions() => [];
        public FunctionInfo? GetPropertyFunctionInfo(MSBuildValueKind valueKind, string name) => null;
        public IEnumerable<FunctionInfo> GetPropertyFunctionNameCompletions(ExpressionNode triggerExpression) => [];
        public FunctionInfo? GetStaticPropertyFunctionInfo(string className, string name) => null;
        public MSBuildValueKind ResolveType(ExpressionPropertyNode node) => MSBuildValueKind.Unknown;
    }
}
