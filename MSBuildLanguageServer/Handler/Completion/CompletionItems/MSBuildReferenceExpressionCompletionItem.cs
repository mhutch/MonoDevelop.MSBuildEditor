// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Roslyn.LanguageServer.Protocol;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler.Completion.CompletionItems;

class MSBuildReferenceExpressionCompletionItem(string text, string description, CompletionItemKind itemKind) : ILspCompletionItem
{
    public bool IsMatch(CompletionItem request) => string.Equals(text, request.Label, StringComparison.Ordinal);

    public ValueTask<CompletionItem> Render(CompletionRenderSettings settings, CompletionRenderContext ctx, CancellationToken cancellationToken)
    {
        var item = new CompletionItem { Label = text };

        if(settings.IncludeItemKind)
        {
            item.Kind = itemKind;
        }

        if(settings.IncludeDocumentation)
        {
            item.Documentation = new MarkupContent {
                Value = description,
                Kind = MarkupKind.Markdown
            };
        }

        if (settings.SupportSnippetFormat && settings.IncludeInsertTextFormat && settings.IncludeTextEdit)
        {
            item.InsertTextFormat = InsertTextFormat.Snippet;

            // TODO: calculate whether this would unbalance the expression
            item.TextEdit = new TextEdit {
                NewText = $"{text}$0)",
                Range = ctx.EditRange
            };
        }

        //TODO: custom commit support. we should be retriggering completion and enabling overtype support for the paren.
        //See MSBuildCompletionCommitManager. TryCommitItemKind

        return ValueTask.FromResult(item);
    }
}
