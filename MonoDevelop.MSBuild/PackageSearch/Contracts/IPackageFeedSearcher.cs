// Copyright (c).NET Foundation and Contributors
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;

namespace ProjectFileTools.NuGetSearch.Contracts;

public interface IPackageFeedSearcher
{
    Task<IPackageNameSearchResult> SearchPackagesAsync(string prefix, params string[] feeds);

    Task<IPackageVersionSearchResult> SearchVersionsAsync(string prefix, params string[] feeds);
}
