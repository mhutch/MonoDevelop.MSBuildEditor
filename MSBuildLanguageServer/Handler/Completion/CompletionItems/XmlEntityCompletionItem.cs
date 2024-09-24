// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Roslyn.LanguageServer.Protocol;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler.Completion.CompletionItems;

class XmlEntityCompletionItem(string name, string character) : ILspCompletionItem
{
    readonly string label = $"&{name};";

    public bool IsMatch(CompletionItem request) => string.Equals(request.Label, label, StringComparison.Ordinal);


    //TODO: need to tweak semicolon insertion for XmlCompletionItemKind.Entity
    public ValueTask<CompletionItem> Render(CompletionRenderSettings settings, CompletionRenderContext ctx, CancellationToken cancellationToken)
    {
        var item = new CompletionItem { Label = label, FilterText = name, Kind = XmlToLspCompletionItemKind.Entity };

        if(settings.IncludeDocumentation)
        {
            item.Documentation = $"Escaped '{character}'";
        };

        return new(item);
    }
}
