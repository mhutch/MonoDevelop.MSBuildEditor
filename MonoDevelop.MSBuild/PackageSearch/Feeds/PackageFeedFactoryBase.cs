// Copyright (c).NET Foundation and Contributors
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using ProjectFileTools.NuGetSearch.Contracts;

namespace ProjectFileTools.NuGetSearch.Feeds;

internal abstract class PackageFeedFactoryBase : IPackageFeedFactory
{
    private static readonly ConcurrentDictionary<string, IPackageFeed> Instances = new ConcurrentDictionary<string, IPackageFeed>();

    protected abstract bool CanHandle(string feed);

    protected abstract IPackageFeed Create(string feed);

    public virtual bool TryHandle(string feed, out IPackageFeed instance)
    {
        if (!CanHandle(feed))
        {
            instance = null;
            return false;
        }

        instance = Instances.GetOrAdd(feed, Create);
        return true;
    }
}
