// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;

using MonoDevelop.MSBuild.Editor.LanguageServer.Handler.Completion.CompletionItems;
using MonoDevelop.MSBuild.Language;

using ProjectFileTools.NuGetSearch.Contracts;
using ProjectFileTools.NuGetSearch.Feeds;
using MonoDevelop.MSBuild.PackageSearch;

using Roslyn.LanguageServer.Protocol;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler.Completion;

class MSBuildCompletionDocsProvider(DisplayElementRenderer renderer, MSBuildRootDocument document, SourceText sourceText, MSBuildResolveResult resolveResult)
{
    internal static MSBuildCompletionDocsProvider Create(ILogger logger, ClientCapabilities clientCapabilities, ClientInfo? clientInfo, MSBuildRootDocument document, SourceText sourceText, MSBuildResolveResult rr)
    {
        var documentationFormat = clientCapabilities.TextDocument?.Completion?.CompletionItem?.DocumentationFormat;
        var renderer = new DisplayElementRenderer(logger, clientCapabilities, clientInfo, documentationFormat);
        return new MSBuildCompletionDocsProvider(renderer, document, sourceText, rr);
    }

    public async Task<MarkupContent?> GetDocumentation(ISymbol symbol, CancellationToken cancellationToken)
    {
        var tooltipContent = await renderer.GetInfoTooltipElement(sourceText, document, symbol, resolveResult, false, cancellationToken);
        if(tooltipContent is not null)
        {
            return new MarkupContent {
                //Value = "<code>$(symbol-keyword) <span style='color:#569cd6;'>keyword</span> <span style='color:#9CDCFE;'>Choose</span></code>\r\n\r\nGroups When and Otherwise elements", // tooltipContent,
                Value = tooltipContent,
                Kind = MarkupKind.Markdown
            };
        }
        return null;
    }

    public async Task<MarkupContent?> GetPackageDocumentation(IPackageSearchManager packageSearchManager, string packageId, string? packageVersion, FeedKind feedKind, string? targetFrameworkSearchParameter, CancellationToken cancellationToken)
    {
        var packageInfos = await packageSearchManager.SearchPackageInfo(packageId, null, targetFrameworkSearchParameter).ToTask(cancellationToken);
        var packageInfo = packageInfos.FirstOrDefault(p => p.SourceKind == feedKind) ?? packageInfos.FirstOrDefault();

        var tooltipContent = renderer.GetPackageInfoTooltip(packageId, packageInfo);
        if(tooltipContent is not null)
        {
            return new MarkupContent {
                Value = tooltipContent,
                Kind = MarkupKind.Markdown
            };
        }
        return null;
    }
}
