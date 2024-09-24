// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Schema;

using Roslyn.LanguageServer.Protocol;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler.Completion.CompletionItems;

class MSBuildCompletionItem(
    ISymbol symbol, XmlCommitKind xmlCommitKind,
    MSBuildCompletionDocsProvider docsProvider,
    string? prefix = null, string? annotation = null, string? sortText = null, bool addDescriptionHint = false
    ) : ILspCompletionItem
{
    string label => prefix is not null ? prefix + symbol.Name : symbol.Name;

    public bool IsMatch(CompletionItem request) => string.Equals(request.Label, label, StringComparison.Ordinal);

    public async ValueTask<CompletionItem> Render(CompletionRenderSettings settings, CompletionRenderContext ctx, CancellationToken cancellationToken)
    {
        var item = new CompletionItem { Label = label, SortText = sortText };

        if(settings.IncludeDeprecatedPropertyOrTag && symbol.IsDeprecated())
        {
            settings.SetDeprecated(item);
        }

        if(settings.IncludeItemKind)
        {
            item.Kind = symbol.GetCompletionItemKind();
        }

        if(annotation is not null)
        {
            item.FilterText = $"{symbol.Name} {annotation}";
            if(settings.IncludeLabelDetails)
            {
                item.LabelDetails = new CompletionItemLabelDetails { Description = annotation };
            }
        } else if(addDescriptionHint)
        {
            if(settings.IncludeLabelDetails)
            {
                var descriptionHint = DescriptionFormatter.GetCompletionHint(symbol);
                item.LabelDetails = new CompletionItemLabelDetails { Description = descriptionHint };
            }
        }

        // TODO: generate completion edit based on the xmlCompletionItemKind

        if(settings.IncludeDocumentation)
        {
            var tooltipContent = await docsProvider.GetDocumentation(symbol, cancellationToken);
            if(tooltipContent is not null)
            {
                //Value = "<code>$(symbol-keyword) <span style='color:#569cd6;'>keyword</span> <span style='color:#9CDCFE;'>Choose</span></code>\r\n\r\nGroups When and Otherwise elements", // tooltipContent,
                item.Documentation = tooltipContent;
            }
        }

        return item;
    }
}