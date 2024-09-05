// Copyright (c).NET Foundation and Contributors
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using ProjectFileTools.NuGetSearch.Feeds;

namespace ProjectFileTools.NuGetSearch.Contracts;

public interface IPackageVersionSearchResult
{
    bool Success { get; }

    IReadOnlyList<string> Versions { get; }

    FeedKind SourceKind { get; }
}
