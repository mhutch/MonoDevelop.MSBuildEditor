// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using ProjectFileTools.NuGetSearch.Contracts;
using ProjectFileTools.NuGetSearch.Feeds;

using Roslyn.LanguageServer.Protocol;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler.Completion;

record class PackageCompletionDocsProvider(IPackageSearchManager PackageSearchManager, MSBuildCompletionDocsProvider docsProvider, string? TargetFrameworkSearchParameter)
{
    public Task<MarkupContent?> GetPackageDocumentation(string packageId, string? packageVersion, FeedKind feedKind, CancellationToken cancellationToken)
        => docsProvider.GetPackageDocumentation(PackageSearchManager, packageId, packageVersion, feedKind, TargetFrameworkSearchParameter, cancellationToken);
}