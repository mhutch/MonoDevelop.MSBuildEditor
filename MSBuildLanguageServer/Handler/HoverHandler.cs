// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Composition;

using Microsoft.CodeAnalysis.LanguageServer.Handler;

using Roslyn.LanguageServer.Protocol;
using Range = Roslyn.LanguageServer.Protocol.Range;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler;

[ExportCSharpVisualBasicStatelessLspService(typeof(HoverHandler)), Shared]
[Method(Methods.TextDocumentHoverName)]
internal sealed class HoverHandler : ILspServiceDocumentRequestHandler<TextDocumentPositionParams, Hover?>
{
    public bool MutatesSolutionState => false;

    public bool RequiresLSPSolution => true;

    public TextDocumentIdentifier GetTextDocumentIdentifier(TextDocumentPositionParams request) => request.TextDocument;

    public async Task<Hover?> HandleRequestAsync(TextDocumentPositionParams request, RequestContext context, CancellationToken cancellationToken)
    {
        return new Hover {
            Range = new Range {
                Start = new Position { Line = 0, Character = 1, },
                End = new Position { Line = 0, Character = 4, }
            },
            Contents = new MarkupContent {
                Kind = MarkupKind.Markdown,
                Value = "Hello"
            }
        };
    }
}
