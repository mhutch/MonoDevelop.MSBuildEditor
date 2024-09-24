// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Composition;
using System.Text.Json;

using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.LanguageServer;
using Microsoft.CodeAnalysis.LanguageServer.Handler;

using MonoDevelop.MSBuild.Editor.LanguageServer.Services;

using Roslyn.LanguageServer.Protocol;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler.CodeActions;

[ExportCSharpVisualBasicStatelessLspService(typeof(CodeActionResolveHandler)), Shared]
[Method(Methods.CodeActionResolveName)]
[method: ImportingConstructor]
[method: Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
sealed class CodeActionResolveHandler() : ILspServiceRequestHandler<CodeAction, CodeAction>
{
    public bool MutatesSolutionState => false;

    public bool RequiresLSPSolution => false;

    public async Task<CodeAction> HandleRequestAsync(CodeAction request, RequestContext context, CancellationToken cancellationToken)
    {
        if(request.Data is not JsonElement data)
        {
            throw new InvalidOperationException("Code item is missing resolve data");
        }

        var resolveData = data.Deserialize<CodeActionResolveData>(ProtocolConversions.LspJsonSerializerOptions);
        if(resolveData?.ResultId == null)
        {
            throw new InvalidOperationException("Code action resolve data is missing resultId");
        }

        var codeActionCache = context.GetRequiredService<CodeActionCache>();

        var codeActionList = codeActionCache.GetCachedEntry(resolveData.ResultId);
        if(codeActionList == null)
        {
            throw new InvalidOperationException("Did not find cached code action list");
        }

        if(resolveData.Index >= codeActionList.Count)
        {
            throw new InvalidOperationException("Code action resolve data has invalid index for cached code action list");
        }

        var codeAction = codeActionList[resolveData.Index];

        var edit = await codeAction.ComputeOperationsAsync(cancellationToken);

        var clientCapabilities = context.GetRequiredClientCapabilities();

        bool includeAnnotations = clientCapabilities.TextDocument?.CodeAction?.HonorsChangeAnnotations ?? false;

        var convertedEdit = edit.ToLspWorkspaceEdit(includeAnnotations, clientCapabilities);

        return new CodeAction {
            Title = codeAction.Title,
            Edit = convertedEdit
        };
    }
}