// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.LanguageServer;

using MonoDevelop.Xml.Tests.Utils;

using Roslyn.Test.Utilities;

using Xunit.Abstractions;

using LSP = Roslyn.LanguageServer.Protocol;

namespace MonoDevelop.MSBuild.LanguageServer.Tests.Completion;

public class CompletionTests(ITestOutputHelper testOutputHelper) : AbstractLanguageServerProtocolTests(testOutputHelper)
{
    [Fact]
    public async Task TestGetCompletionAsync()
    {
        var fsw = new LSP.FileSystemWatcher {
            GlobPattern = new LSP.RelativePattern {
                BaseUri = ProtocolConversions.CreateRelativePatternBaseUri("C:\\Users"),
                Pattern = ".*"
            }
        };

        (string documentText, int caretOffset) = TextWithMarkers.ExtractSinglePosition (@"<Pro|ject></Project>", '|');
        var caretPos = ConvertPosition(caretOffset, documentText);

        var capabilities = new LSP.ClientCapabilities {
            TextDocument = new LSP.TextDocumentClientCapabilities {
                Completion = new LSP.CompletionSetting ()
            }
        };

        InitializationOptions initializationOptions = new() {
            ClientCapabilities = capabilities,
            ClientMessageFormatter = RoslynLanguageServer.CreateJsonMessageFormatter(excludeVSExtensionConverters: true)
        };

        await using var testLspServer = await CreateTestLspServerAsync(documentText, false, initializationOptions);

        var documentUri = new Uri("file://foo.csproj");

        await testLspServer.OpenDocument(documentUri, documentText);

        var completionList = await testLspServer.GetCompletionList(documentUri, caretPos);

        var firstItem = completionList.Items[0];
        Assert.Equal("Project", firstItem.Label);
        Assert.Null(firstItem.Documentation);

        var resolved = await testLspServer.ResolveCompletionItem(firstItem);
        Assert.NotNull(resolved.Documentation);
    }

    static LSP.Position ConvertPosition(int offset, string documentText)
    {
        int line = 0, col = 0;
        for(int i = 0; i < offset; i++)
        {
            char ch = documentText[i];
            if(ch == '\n')
            {
                line++;
                col = 0;
            }
            else
            {
                col++;
            }
        }
        return new LSP.Position(line, col);
    }
}
