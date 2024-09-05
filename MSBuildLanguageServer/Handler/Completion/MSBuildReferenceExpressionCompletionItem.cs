﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Roslyn.LanguageServer.Protocol;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler.Completion;

class MSBuildReferenceExpressionCompletionItem(string text, string description, CompletionItemKind itemKind) : ILspCompletionItem
{
    public bool IsMatch(CompletionItem request) => string.Equals(text, request.Label, StringComparison.Ordinal);

    public async ValueTask<CompletionItem> Render(CompletionRenderSettings settings, CancellationToken cancellationToken)
    {
        var item = new CompletionItem { Label = text };

        if (settings.IncludeItemKind)
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

        //TODO: custom commit support. we should be retriggering completion and enabling overtype support for the paren.
        //See MSBuildCompletionCommitManager. TryCommitItemKind

        return item;
    }
}