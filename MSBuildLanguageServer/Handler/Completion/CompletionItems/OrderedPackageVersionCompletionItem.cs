// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ProjectFileTools.NuGetSearch.Feeds;

using Roslyn.LanguageServer.Protocol;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler.Completion.CompletionItems;

class OrderedPackageVersionCompletionItem(
    int index,
    string packageId,
    Tuple<string, FeedKind> packageVersionAndKind,
    PackageCompletionDocsProvider docsProvider
    ) : ILspCompletionItem
{
    string packageVersion => packageVersionAndKind.Item1;
    FeedKind packageKind => packageVersionAndKind.Item2;

    public bool IsMatch(CompletionItem request) => string.Equals(request.Label, packageVersion, StringComparison.Ordinal);

    public async ValueTask<CompletionItem> Render(CompletionRenderSettings settings, CancellationToken cancellationToken)
    {
        var item = new CompletionItem {
            Label = packageVersion,
            SortText = $"_{index:D5}"
        };

        if(settings.IncludeItemKind)
        {
            item.Kind = packageKind.GetCompletionItemKind();
        }

        // TODO: generate completion edit

        if(settings.IncludeDocumentation)
        {
            item.Documentation = await docsProvider.GetPackageDocumentation(packageId, packageVersion, packageKind, cancellationToken);
        }

        return item;
    }
}