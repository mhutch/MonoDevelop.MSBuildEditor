// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Roslyn.LanguageServer.Protocol;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler.Completion.CompletionItems;

class XmlCompletionItem(string label, CompletionItemKind kind, string markdownDocumentation, XmlCommitKind commitKind) : ILspCompletionItem
{
    public bool IsMatch(CompletionItem request) => string.Equals(request.Label, label, StringComparison.Ordinal);

    public ValueTask<CompletionItem> Render(CompletionRenderSettings settings, CompletionRenderContext ctx, CancellationToken cancellationToken)
    {
        var item = new CompletionItem { Label = label, Kind = kind };

        if(settings.IncludeDocumentation)
        {
            // TODO: strip markdown if client only supports text
            item.Documentation = markdownDocumentation;
        }

        // TODO: generate completion edit based on CommitKind

        return new(item);
    }

    static MarkupContent CreateMarkdown(string markdown)
        => new() { Kind = MarkupKind.Markdown, Value = markdown };
}
