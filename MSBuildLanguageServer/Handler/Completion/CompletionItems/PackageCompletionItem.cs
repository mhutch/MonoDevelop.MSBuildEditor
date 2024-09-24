// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ProjectFileTools.NuGetSearch.Feeds;

using Roslyn.LanguageServer.Protocol;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler.Completion.CompletionItems;

class PackageNameCompletionItem(
    Tuple<string, FeedKind> packageNameAndKind,
    PackageCompletionDocsProvider docsProvider
    ) : ILspCompletionItem
{
    string packageId => packageNameAndKind.Item1;
    FeedKind packageKind => packageNameAndKind.Item2;

    public bool IsMatch(CompletionItem request) => string.Equals(request.Label, packageId, StringComparison.Ordinal);

    public async ValueTask<CompletionItem> Render(CompletionRenderSettings settings, CompletionRenderContext ctx, CancellationToken cancellationToken)
    {
        var item = new CompletionItem { Label = packageId };

        if(settings.IncludeItemKind)
        {
            item.Kind = packageKind.GetCompletionItemKind();
        }

        // TODO: generate completion edit

        if(settings.IncludeDocumentation)
        {
            item.Documentation = await docsProvider.GetPackageDocumentation(packageId, null, packageKind, cancellationToken);
        }

        return item;
    }
}
