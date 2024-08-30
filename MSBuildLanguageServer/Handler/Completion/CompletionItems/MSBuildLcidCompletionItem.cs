// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.MSBuild.Language;

using Roslyn.LanguageServer.Protocol;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler.Completion.CompletionItems;

class MSBuildLcidCompletionItem(KnownCulture culture, MSBuildCompletionDocsProvider docsProvider) : ILspCompletionItem
{
    readonly string label = culture.Lcid.ToString();

    public bool IsMatch(CompletionItem request) => string.Equals(request.Label, label, StringComparison.Ordinal);

    public async ValueTask<CompletionItem> Render(CompletionRenderSettings settings, CancellationToken cancellationToken)
    {
        var item = new CompletionItem { Label = label };

        if(settings.IncludeItemKind)
        {
            item.Kind = MSBuildCompletionItemKind.Culture;
        }

        // add the culture name to the filter text so ppl can just type the actual language/country instead of looking up the code
        item.FilterText = $"{label} {culture.DisplayName}";

        if(settings.IncludeLabelDetails)
        {
            item.LabelDetails = new CompletionItemLabelDetails { Description = culture.DisplayName };
        }

        if(settings.IncludeDocumentation)
        {
            item.Documentation = await docsProvider.GetDocumentation(culture.CreateCultureSymbol(), cancellationToken);
        }

        return item;
    }
}
