// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Composition;

using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.LanguageServer;
using Microsoft.CodeAnalysis.LanguageServer.Handler;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CommonLanguageServerProtocol.Framework;

using MonoDevelop.MSBuild.Editor.CodeActions;
using MonoDevelop.MSBuild.Editor.LanguageServer.Services;
using MonoDevelop.Xml.Options;

using Roslyn.LanguageServer.Protocol;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler.CodeActions;

[ExportCSharpVisualBasicStatelessLspService(typeof(CodeActionHandler)), Shared]
[Method(Methods.TextDocumentCodeActionName)]
[method: ImportingConstructor]
[method: Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
sealed class CodeActionHandler(MSBuildCodeActionService codeActionService)
    : ILspServiceDocumentRequestHandler<CodeActionParams, SumType<Command, CodeAction>[]?>
{
    public bool MutatesSolutionState => false;

    public bool RequiresLSPSolution => true;

    public TextDocumentIdentifier GetTextDocumentIdentifier(CodeActionParams request) => request.TextDocument;

    public async Task<SumType<Command, CodeAction>[]?> HandleRequestAsync(CodeActionParams request, RequestContext context, CancellationToken cancellationToken)
    {
        var clientCapabilities = context.GetRequiredClientCapabilities();

        if(clientCapabilities.TextDocument?.CodeAction?.CodeActionLiteralSupport is not { } literalSupport)
        {
            throw new InvalidOperationException("Code action handler should not be registered if client doesn't support CodeAction literals");
        }

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

        var msbuildDoc = result.MSBuildDocument;
        var sourceText = result.XmlParseResult.Text;

        var roslynSpan = ProtocolConversions.RangeToTextSpan(request.Range, sourceText);
        Xml.Dom.TextSpan span = new(roslynSpan.Start, roslynSpan.Length);

        var rawRequestedKinds = request.Context.Only ?? literalSupport.CodeActionKind.ValueSet;
        ISet<MSBuildCodeActionKind> requestedKinds = rawRequestedKinds.GetMSBuildCodeActionKinds();

        // TODO: get options from client
        var options = new EmptyOptionsReader();

        var fixes = await codeActionService.GetCodeActions(sourceText, msbuildDoc, span, requestedKinds, options, cancellationToken);

        if(fixes is null || fixes.Count == 0)
        {
            return null;
        }

        return await ConvertCodeActions(fixes, context, sourceText, clientCapabilities, cancellationToken);
    }

    async Task<SumType<Command, CodeAction>[]> ConvertCodeActions(List<MSBuildCodeAction> codeActions, RequestContext context, SourceText sourceText, ClientCapabilities clientCapabilities, CancellationToken cancellationToken)
    {
        bool resolveSupport = false;
        bool includeEdit = true;

        if (clientCapabilities.TextDocument?.CodeAction?.ResolveSupport is { } resolveSupportSetting)
        {
            resolveSupport = true;

            if(resolveSupportSetting.Properties is { } resolveProperties)
            {
                includeEdit = !resolveProperties.Contains("edit", StringComparer.OrdinalIgnoreCase);
            }
        }

        bool includeAnnotations = includeEdit && (clientCapabilities.TextDocument?.CodeAction?.HonorsChangeAnnotations ?? false);

        var results = new SumType<Command, CodeAction>[codeActions.Count];

        // TODO: include diagnostics
        // TODO: can we reuse request.Context.Diagnostics? they may be outdated

        Diagnostic[]? ConvertDiagnostics(MSBuildCodeAction codeAction)
        {
            if (codeAction.FixesDiagnostics.Count == 0)
            {
                return null;
            }

            var converted = new Diagnostic[codeAction.FixesDiagnostics.Count];
            for (int i = 0; i < converted.Length; i++)
            {
                converted[i] = codeAction.FixesDiagnostics[i].ToLspDiagnostic(sourceText);
            }

            return converted;
        }

        if(includeEdit)
        {
            await Parallel.ForAsync(0, codeActions.Count, cancellationToken, async (i, ct) => {
                var codeAction = codeActions[i];
                var edit = await codeAction.ComputeOperationsAsync(ct);
                var convertedEdit = edit.ToLspWorkspaceEdit(includeAnnotations, clientCapabilities);
                results[i] = new CodeAction {
                    Title = codeAction.Title,
                    Kind = codeAction.GetLspCodeActionKind(),
                    Edit = convertedEdit, 
                    Diagnostics = ConvertDiagnostics(codeAction)
                };
            });
        } else
        {
            for(int i = 0; i < codeActions.Count; i++)
            {
                var codeAction = codeActions[i];
                results[i] = new CodeAction {
                    Title = codeAction.Title,
                    Kind = codeAction.GetLspCodeActionKind(),
                    Diagnostics = ConvertDiagnostics(codeAction)
                };
            }
        }

        if(resolveSupport)
        {
            var resolveCache = context.GetRequiredLspService<CodeActionCache>();

            long resultCacheId = resolveCache.UpdateCache(codeActions);

            for(int i = 0; i < results.Length; i++)
            {
                results[i].Second.Data = new CodeActionResolveData { ResultId = resultCacheId, Index = i };
            }
        }

        return results;
    }
}

class EmptyOptionsReader : IOptionsReader
{
    public bool TryGetOption<T>(Option<T> option, out T? value)
    {
        value = default;
        return false;
    }
}
