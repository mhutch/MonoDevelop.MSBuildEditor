// Copyright (c).NET Foundation and Contributors
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace ProjectFileTools.NuGetSearch.Contracts;

public interface IPackageFeedFactorySelector
{
    IEnumerable<IPackageFeedFactory> FeedFactories { get; }

    IPackageFeed GetFeed(string source);
}
