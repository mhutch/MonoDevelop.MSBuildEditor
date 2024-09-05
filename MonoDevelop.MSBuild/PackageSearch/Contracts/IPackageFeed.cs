// Copyright (c).NET Foundation and Contributors
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;

namespace ProjectFileTools.NuGetSearch.Contracts;

public interface IPackageFeed
{
    string DisplayName { get; }

    Task<IPackageNameSearchResult> GetPackageNamesAsync(string prefix, IPackageQueryConfiguration queryConfiguration, CancellationToken cancellationToken);

    Task<IPackageVersionSearchResult> GetPackageVersionsAsync(string id, IPackageQueryConfiguration queryConfiguration, CancellationToken cancellationToken);

    Task<IPackageInfo> GetPackageInfoAsync(string id, string? version, IPackageQueryConfiguration queryConfiguration, CancellationToken cancellationToken);
}
