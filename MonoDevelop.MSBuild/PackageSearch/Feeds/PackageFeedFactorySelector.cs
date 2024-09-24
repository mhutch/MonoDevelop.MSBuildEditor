// Copyright (c).NET Foundation and Contributors
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using ProjectFileTools.NuGetSearch.Contracts;

namespace ProjectFileTools.NuGetSearch.Feeds;

public class PackageFeedFactorySelector : IPackageFeedFactorySelector
{
    private readonly ConcurrentDictionary<string, IPackageFeed> _feedCache = new ConcurrentDictionary<string, IPackageFeed>(StringComparer.OrdinalIgnoreCase);

    public PackageFeedFactorySelector(IEnumerable<IPackageFeedFactory> feedFactories)
    {
        FeedFactories = feedFactories;
    }

    public IEnumerable<IPackageFeedFactory> FeedFactories { get; }

    public IPackageFeed GetFeed(string source)
    {
        if (_feedCache.TryGetValue(source, out IPackageFeed match))
        {
            return match;
        }

        foreach(IPackageFeedFactory feed in FeedFactories)
        {
            if (feed.TryHandle(source, out IPackageFeed instance))
            {
                _feedCache[source] = instance;
                return instance;
            }
        }

        _feedCache[source] = null;
        return null;
    }
}
