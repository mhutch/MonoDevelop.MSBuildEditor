// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.LanguageServer;
using MonoDevelop.Xml.Tests.Utils;
using LSP = Roslyn.LanguageServer.Protocol;

using static Roslyn.Test.Utilities.AbstractLanguageServerProtocolTests;

namespace MonoDevelop.MSBuild.LanguageServer.Tests;

static class TestServerExtensions
{
    public static async Task<LSP.CompletionList> OpenDocumentAndGetCompletionList(this TestLspServer testLspServer, string documentText, char caretMarker = '$', string? filename = default, CancellationToken cancellationToken = default)
    {
        (documentText, var caret) = TextWithMarkers.ExtractSingleLineColPosition(documentText, '$');

        var documentUri = new Uri(filename ?? "file://foo.csproj");

        await testLspServer.OpenDocument(documentUri, documentText, cancellationToken);

        return await testLspServer.GetCompletionList(documentUri, caret.AsLspPosition(), cancellationToken: cancellationToken);
    }

    public static async Task OpenDocument(this TestLspServer testLspServer, Uri documentUri, string documentText, CancellationToken cancellationToken = default)
    {
        await testLspServer.ExecuteRequestAsync<LSP.DidOpenTextDocumentParams, object>(
            LSP.Methods.TextDocumentDidOpenName,
            new LSP.DidOpenTextDocumentParams {
                TextDocument = new LSP.TextDocumentItem {
                    Text = documentText,
                    Uri = documentUri,
                    LanguageId = LanguageName.MSBuild,
                    Version = 0
                }
            },
            cancellationToken);
    }

    public static async Task<LSP.CompletionList> GetCompletionList(
        this TestLspServer testLspServer,
        Uri documentUri,
        LSP.Position caretPosition,
        LSP.CompletionTriggerKind? triggerKind = null,
        char? triggerChar = null,
        CancellationToken cancellationToken = default)
    {
        var completionParams = new LSP.CompletionParams {
            TextDocument = new LSP.TextDocumentIdentifier { Uri = documentUri },
            Position = caretPosition
        };

        if (triggerChar.HasValue || triggerKind.HasValue)
        {
            completionParams.Context = new LSP.CompletionContext {
                TriggerCharacter = triggerChar?.ToString(),
                TriggerKind = triggerKind ?? ( triggerChar.HasValue? LSP.CompletionTriggerKind.TriggerCharacter : LSP.CompletionTriggerKind.Invoked)
            };
        }

        var result = await testLspServer.ExecuteRequestAsync<LSP.CompletionParams, LSP.SumType<LSP.CompletionItem[], LSP.CompletionList>>(
            LSP.Methods.TextDocumentCompletionName,
            completionParams,
            cancellationToken);

        var completionList = Assert.IsType<LSP.CompletionList>(result.Value);
        return completionList;
    }

    public static async Task<LSP.CompletionItem> ResolveCompletionItem(this TestLspServer testLspServer, LSP.CompletionItem item, CancellationToken cancellationToken = default)
    {
        var resolved = await testLspServer.ExecuteRequestAsync<LSP.CompletionItem, LSP.CompletionItem>(
            LSP.Methods.TextDocumentCompletionResolveName,
            item,
            CancellationToken.None);
        Assert.NotNull(resolved);
        return resolved;
    }

    public static LSP.Position AsLspPosition(this TextMarkerPosition position) => new LSP.Position { Line = position.Line, Character = position.Column }; 
}
