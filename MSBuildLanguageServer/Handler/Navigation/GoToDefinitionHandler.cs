// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Composition;

using Microsoft.CodeAnalysis.LanguageServer;
using Microsoft.CodeAnalysis.LanguageServer.Handler;
using Microsoft.CommonLanguageServerProtocol.Framework;

using MonoDevelop.MSBuild.Editor.LanguageServer.Parser;
using MonoDevelop.MSBuild.Editor.LanguageServer.Services;
using MonoDevelop.MSBuild.Editor.LanguageServer.Workspace;
using MonoDevelop.MSBuild.Editor.Navigation;
using MonoDevelop.MSBuild.Language;

using Roslyn.LanguageServer.Protocol;

using LSP = Roslyn.LanguageServer.Protocol;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler.Navigation;

[ExportCSharpVisualBasicStatelessLspService(typeof(GoToDefinitionHandler)), Shared]
[Method(Methods.TextDocumentDefinitionName)]
sealed class GoToDefinitionHandler : ILspServiceDocumentRequestHandler<DefinitionParams, SumType<Location, Location[], LocationLink[]>?>
{
    public bool MutatesSolutionState => false;

    public bool RequiresLSPSolution => true;

    public TextDocumentIdentifier GetTextDocumentIdentifier(DefinitionParams request) => request.TextDocument;

    public async Task<SumType<Location, Location[], LocationLink[]>?> HandleRequestAsync(DefinitionParams request, RequestContext context, CancellationToken cancellationToken)
    {
        var logger = context.GetRequiredService<ILspLogger>();
        var extLogger = logger.ToILogger();

        var msbuildParserService = context.GetRequiredService<LspMSBuildParserService>();

        var document = context.GetRequiredDocument();

        if(!msbuildParserService.TryGetParseResult(document.CurrentState, out Task<MSBuildParseResult>? parseTask, cancellationToken))
        {
            return null;
        }

        var parseResult = await parseTask!; // not sure why we need the ! here, TryGetParseResult has NotNullWhen(true)

        if(parseResult?.MSBuildDocument is not MSBuildRootDocument doc)
        {
            return null;
        }

        var sourceText = parseResult.XmlParseResult.Text;
        var position = ProtocolConversions.PositionToLinePosition(request.Position);
        int offset = position.ToOffset(sourceText);

        var spineParser = parseResult.XmlParseResult.GetSpineParser(position, cancellationToken);

        var functionTypeProvider = context.GetRequiredService<FunctionTypeProviderService>().FunctionTypeProvider;

        //FIXME: can we avoid awaiting this unless we actually need to resolve a function? need to propagate async downwards
        await functionTypeProvider.EnsureInitialized(cancellationToken);

        var rr = MSBuildResolver.Resolve(spineParser, parseResult.XmlParseResult.Text.GetTextSource(), parseResult.MSBuildDocument, functionTypeProvider, extLogger, cancellationToken);
        if(rr is null)
        {
            return null;
        }

        var result = MSBuildNavigation.GetNavigation(doc, offset, rr);
        if(result is null)
        {
            return null;
        }

        var originRange = parseResult.XmlParseResult.Text.GetLspRange(result.Offset, result.Length);

        var locations = await GetGoToDefinitionLocations(result, parseResult, originRange, request, context, cancellationToken);

        var linkSupport = context.GetRequiredClientCapabilities().TextDocument?.Definition?.LinkSupport ?? false;

        return LocationHelpers.ConvertLocationLinksToLocationsIfNeeded(locations, linkSupport);
    }

    static async Task<LocationLink[]?> GetGoToDefinitionLocations(
        MSBuildNavigationResult result,
        MSBuildParseResult originParseResult,
        LSP.Range originRange,
        DefinitionParams request,
        RequestContext context,
        CancellationToken cancellationToken)
    {
        if(result.Paths != null)
        {
            if(result.Paths.Length == 1)
            {
                return [LocationHelpers.CreateLocationLink(originRange, result.Paths[0])];
            }
            if(result.Paths.Length > 1)
            {
                return result.Paths.Select(p => LocationHelpers.CreateLocationLink(originRange, p)).ToArray();
            }
        }

        if(result.DestFile != null)
        {
            var workspace = context.GetRequiredLspService<LspEditorWorkspace>();
            var targetRange = LocationHelpers.ConvertRangeViaWorkspace(workspace, result.DestFile, result.TargetSpan);
            return [LocationHelpers.CreateLocationLink(originRange, result.DestFile, targetRange)];
        }

        Func<FindReferencesResult, bool>? resultFilter = null;
        MSBuildReferenceCollectorFactory collectorFactory;

        switch(result.Kind)
        {
        case MSBuildReferenceKind.Target:
            request.WorkDoneToken?.Begin($"Finding definitions for target '{result.Name}'");
            collectorFactory = (doc, text, logger, reporter) => new MSBuildTargetDefinitionCollector(doc, text, logger, result.Name!, reporter);
            break;
        case MSBuildReferenceKind.Item:
            request.WorkDoneToken?.Begin($"Finding assignments for item '{result.Name}'");
            collectorFactory = (doc, text, logger, reporter) => new MSBuildItemReferenceCollector(doc, text, logger, result.Name!, reporter);
            resultFilter = MSBuildNavigationHelpers.FilterUsageWrites;
            break;
        case MSBuildReferenceKind.Property:
            request.WorkDoneToken?.Begin($"Finding assignments for property '{result.Name}'");
            collectorFactory = (doc, text, logger, reporter) => new MSBuildPropertyReferenceCollector(doc, text, logger, result.Name!, reporter);
            resultFilter = MSBuildNavigationHelpers.FilterUsageWrites;
            break;
        case MSBuildReferenceKind.NuGetID:
        // TODO: can we navigate to a package URL?
        // OpenNuGetUrl(result.Name, EditorHost, logger);
        default:
            return null;
        }

        var resultReporter = BufferedProgress.Create<LocationLink[], SumType<Location[], LocationLink[]>>(request.PartialResultToken, ll => new(ll));
        var findReferencesService = context.GetRequiredLspService<LspNavigationService>();

        await findReferencesService.FindReferences(
            originParseResult,
            originRange,
            collectorFactory,
            resultReporter,
            request.WorkDoneToken,
            cancellationToken,
            resultFilter).ConfigureAwait(false);

        request.WorkDoneToken?.End();

        return resultReporter.GetFlattenedValues();
    }
}
