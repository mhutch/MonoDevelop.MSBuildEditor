// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Composition;

using Microsoft.CodeAnalysis.LanguageServer;
using Microsoft.CodeAnalysis.LanguageServer.Handler;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.VisualStudio.Composition;

using MonoDevelop.MSBuild.Editor.LanguageServer.Parser;
using MonoDevelop.MSBuild.Editor.LanguageServer.Services;
using MonoDevelop.MSBuild.Editor.LanguageServer.Workspace;
using MonoDevelop.MSBuild.Language;

using Roslyn.LanguageServer.Protocol;

using LSP = Roslyn.LanguageServer.Protocol;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler.Navigation;

[ExportCSharpVisualBasicStatelessLspService(typeof(FindAllReferencesHandler)), Shared]
[Method(Methods.TextDocumentReferencesName)]
sealed class FindAllReferencesHandler : ILspServiceDocumentRequestHandler<ReferenceParams, Location[]?>
{
    public bool MutatesSolutionState => false;

    public bool RequiresLSPSolution => true;

    public TextDocumentIdentifier GetTextDocumentIdentifier(ReferenceParams request) => request.TextDocument;

    public async Task<Location[]?> HandleRequestAsync(ReferenceParams request, RequestContext context, CancellationToken cancellationToken)
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

        if(!MSBuildReferenceCollector.CanCreate(rr))
        {
            return null;
        }

        var resultReporter = BufferedProgress.Create(request.PartialResultToken);
        var navigationService = context.GetRequiredLspService<LspNavigationService>();

        await navigationService.FindReferences(
            parseResult,
            (doc, text, logger, reporter) => MSBuildReferenceCollector.Create(doc, text, logger, rr, functionTypeProvider, reporter),
            resultReporter,
            request.WorkDoneToken,
            cancellationToken
            ).ConfigureAwait(false);

        request.WorkDoneToken?.End();

        return resultReporter.GetFlattenedValues();
    }
}
