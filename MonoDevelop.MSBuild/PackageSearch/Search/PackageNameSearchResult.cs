// Copyright (c).NET Foundation and Contributors
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using ProjectFileTools.NuGetSearch.Contracts;
using ProjectFileTools.NuGetSearch.Feeds;

namespace ProjectFileTools.NuGetSearch.Search;

internal class PackageNameSearchResult : IPackageNameSearchResult
{
    public static IPackageNameSearchResult Cancelled { get; } = new PackageNameSearchResult();

    public static IPackageNameSearchResult Failure { get; } = new PackageNameSearchResult();

    public static Task<IPackageNameSearchResult> CancelledTask { get; } = Task.FromResult(Cancelled);

    public static Task<IPackageNameSearchResult> FailureTask { get; } = Task.FromResult(Failure);

    public bool Success { get; }

    public IReadOnlyList<string> Names { get; }

    public FeedKind SourceKind { get; }

    private PackageNameSearchResult()
    {
    }

    public PackageNameSearchResult(IReadOnlyList<string> names, FeedKind sourceKind)
    {
        Success = true;
        Names = names;
        SourceKind = sourceKind;
    }
}
