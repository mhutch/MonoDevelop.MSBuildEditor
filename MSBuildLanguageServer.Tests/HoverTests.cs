// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.LanguageServer;

using MonoDevelop.Xml.Tests.Utils;

using Roslyn.Test.Utilities;

using Xunit.Abstractions;

using LSP = Roslyn.LanguageServer.Protocol;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Tests;

public class HoverTests(ITestOutputHelper testOutputHelper) : AbstractLanguageServerProtocolTests(testOutputHelper)
{
    [Fact]
    public async Task TestGetHoverAsync()
    {
        (string documentText, int caretPos) = TextWithMarkers.ExtractSinglePosition (@"<Pro|ject></Project>", '|');

        var capabilities = new LSP.ClientCapabilities {
            TextDocument = new LSP.TextDocumentClientCapabilities {
                Hover = new LSP.HoverSetting {
                    ContentFormat = [ LSP.MarkupKind.PlainText ]
                }
            }
        };

        await using var testLspServer = await CreateTestLspServerAsync(documentText, false, capabilities);

        var documentId = new LSP.TextDocumentIdentifier { Uri = new Uri("file://foo.csproj") };

        await testLspServer.ExecuteRequestAsync<LSP.DidOpenTextDocumentParams, object>(
            LSP.Methods.TextDocumentDidOpenName,
            new LSP.DidOpenTextDocumentParams {
                TextDocument = new LSP.TextDocumentItem {
                    Text = documentText,
                    Uri = documentId.Uri,
                    LanguageId = LanguageName.MSBuild,
                    Version = 0
                }
            },
            CancellationToken.None);


        var caret = new LSP.TextDocumentPositionParams {
            TextDocument = documentId,
            Position = new LSP.Position { Line = 0, Character = caretPos }
        };

        var result = await testLspServer.ExecuteRequestAsync<LSP.TextDocumentPositionParams, LSP.Hover>(
            LSP.Methods.TextDocumentHoverName,
            caret,
            CancellationToken.None);

        Assert.NotNull(result);

        var markup = Assert.IsType<LSP.MarkupContent>(result.Contents.Value);
        var markupValue = markup.Value.Replace("\r\n", "\n");

        Assert.Equal("keyword Project\nAn MSBuild project.", markupValue);
    }
}
