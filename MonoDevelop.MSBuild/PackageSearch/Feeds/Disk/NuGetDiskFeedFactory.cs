// Copyright (c).NET Foundation and Contributors
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using ProjectFileTools.NuGetSearch.Contracts;
using ProjectFileTools.NuGetSearch.IO;

namespace ProjectFileTools.NuGetSearch.Feeds.Disk;

public class NuGetDiskFeedFactory : IPackageFeedFactory
{
    private static readonly ConcurrentDictionary<string, IPackageFeed> Instances = new ConcurrentDictionary<string, IPackageFeed>();
    private readonly IFileSystem _fileSystem;

    public NuGetDiskFeedFactory(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public bool TryHandle(string feed, out IPackageFeed instance)
    {
        if (!_fileSystem.DirectoryExists(feed))
        {
            instance = null;
            return false;
        }

        instance = Instances.GetOrAdd(feed, x => CreateInstance(x));
        return instance != null;
    }

    private IPackageFeed CreateInstance(string feed)
    {
        if (IsNuGetV3Feed(feed))
        {
            return Instances.GetOrAdd(feed, x => new NuGetV3DiskFeed(x, _fileSystem));
        }

        if (IsNuGetV2Feed(feed))
        {
            return Instances.GetOrAdd(feed, x => new NuGetV2DiskFeed(x, _fileSystem));
        }

        return null;
    }

    private bool IsNuGetV2Feed(string feed)
    {
        foreach (string dir in _fileSystem.EnumerateDirectories(feed, "*", SearchOption.TopDirectoryOnly))
        {
            if (_fileSystem.EnumerateFiles(dir, "*.nupkg", SearchOption.TopDirectoryOnly).Any())
            {
                return true;
            }
        }

        return false;
    }

    private bool IsNuGetV3Feed(string feed)
    {
        foreach (string dir in _fileSystem.EnumerateDirectories(feed, "*", SearchOption.TopDirectoryOnly))
        {
            if (_fileSystem.EnumerateFiles(dir, "*.nuspec", SearchOption.TopDirectoryOnly).Any())
            {
                return false;
            }

            if(_fileSystem.GetDirectoryName(dir).IndexOf('.') == 0)
            {
                continue;
            }

            foreach (string sub in _fileSystem.EnumerateDirectories(dir, "*", SearchOption.TopDirectoryOnly))
            {
                if (SemanticVersion.Parse(_fileSystem.GetDirectoryName(sub)) == null)
                {
                    return false;
                }

                if (!_fileSystem.EnumerateFiles(sub, "*.nuspec", SearchOption.TopDirectoryOnly).Any())
                {
                    return false;
                }
            }
        }

        return true;
    }
}
