// Copyright (c).NET Foundation and Contributors
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using ProjectFileTools.NuGetSearch.Contracts;
using ProjectFileTools.NuGetSearch.Feeds;

namespace ProjectFileTools.NuGetSearch.Search;

internal class PackageVersionSearchResult : IPackageVersionSearchResult
{
    public static IPackageVersionSearchResult Cancelled { get; } = new PackageVersionSearchResult();

    public static Task<IPackageVersionSearchResult> CancelledTask { get; } = Task.FromResult(Cancelled);

    public static IPackageVersionSearchResult Failure { get; } = new PackageVersionSearchResult();

    public static Task<IPackageVersionSearchResult> FailureTask { get; } = Task.FromResult(Failure);

    public bool Success { get; }

    public IReadOnlyList<string> Versions { get; }

    public FeedKind SourceKind { get; }

    private PackageVersionSearchResult()
    {
    }

    public PackageVersionSearchResult(IReadOnlyList<string> versions, FeedKind kind)
    {
        Versions = versions;
        Success = true;
        SourceKind = kind;
    }
}
