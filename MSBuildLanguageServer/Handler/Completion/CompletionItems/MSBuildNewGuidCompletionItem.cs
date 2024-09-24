// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Roslyn.LanguageServer.Protocol;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler.Completion.CompletionItems;

class MSBuildNewGuidCompletionItem() : ILspCompletionItem
{
    const string label = "New GUID";

    public bool IsMatch(CompletionItem request) => string.Equals(label, request.Label, StringComparison.Ordinal);

    public async ValueTask<CompletionItem> Render(CompletionRenderSettings settings, CompletionRenderContext ctx, CancellationToken cancellationToken)
    {
        var item = new CompletionItem { Label = label };

        if(settings.IncludeItemKind)
        {
            item.Kind = MSBuildCompletionItemKind.NewGuid;
        }

        if(settings.IncludeDocumentation)
        {
            item.Documentation = new MarkupContent {
                Value = "Inserts a new GUID",
                Kind = MarkupKind.Markdown
            };
        }

        if(settings.IncludeInsertText)
        {
            item.InsertText = Guid.NewGuid().ToString("B").ToUpper();
        }

        return item;
    }
}