// Copyright (c).NET Foundation and Contributors
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ProjectFileTools.NuGetSearch.Contracts;
using ProjectFileTools.NuGetSearch.IO;
using ProjectFileTools.NuGetSearch.Search;

namespace ProjectFileTools.NuGetSearch.Feeds.Disk;

internal class NuGetV2DiskFeed : IPackageFeed
{
    private readonly string _feed;
    private readonly bool _isRemote;
    private readonly IFileSystem _fileSystem;

    public NuGetV2DiskFeed(string feed, IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
        _feed = feed;
        _isRemote = Uri.TryCreate(feed, UriKind.Absolute, out Uri uri) && uri.IsUnc;
    }

    public string DisplayName
    {
        get
        {
            if (_isRemote)
            {
                return $"NuGet v2 (Remote: {_feed})";
            }

            return $"NuGet v2 (Local: {_feed})";
        }
    }

    public Task<IPackageInfo> GetPackageInfoAsync(string id, string version, IPackageQueryConfiguration queryConfiguration, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            if (version != null)
            {
                string nuspec = Path.Combine(_feed, $"{id}.{version}", $"{id}.nuspec");

                if (_fileSystem.FileExists(nuspec))
                {
                    return Task.FromResult(NuSpecReader.Read(nuspec, FeedKind.Local));
                }
                else
                {
                    return Task.FromResult<IPackageInfo>(null);
                }
            }
            else
            {
                string dir = _fileSystem.EnumerateDirectories(_feed).OrderByDescending(x => SemanticVersion.Parse(_fileSystem.GetDirectoryNameOnly(x).Substring(id.Length + 1))).FirstOrDefault();

                if (dir == null)
                {
                    return Task.FromResult<IPackageInfo>(null);
                }

                string nuspec = Path.Combine(dir, $"{id}.nuspec");

                if (_fileSystem.FileExists(nuspec))
                {
                    return Task.FromResult(NuSpecReader.Read(nuspec, FeedKind.Local));
                }
                else
                {
                    return Task.FromResult<IPackageInfo>(null);
                }
            }
        }, cancellationToken);
    }

    public Task<IPackageNameSearchResult> GetPackageNamesAsync(string prefix, IPackageQueryConfiguration queryConfiguration, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            try
            {
                List<IPackageInfo> infos = new List<IPackageInfo>();
                foreach (string path in _fileSystem.EnumerateDirectories(_feed).Where(x => x.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) > -1))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return PackageNameSearchResult.Cancelled;
                    }

                    string nuspec = _fileSystem.EnumerateFiles(path, "*.nuspec", SearchOption.TopDirectoryOnly).FirstOrDefault();

                    if (nuspec != null)
                    {
                        IPackageInfo info = NuSpecReader.Read(nuspec, FeedKind.Local);

                        if (info != null && NuGetPackageMatcher.IsMatch(path, info, queryConfiguration, _fileSystem))
                        {
                            infos.Add(info);

                            if (infos.Count >= queryConfiguration.MaxResults)
                            {
                                break;
                            }
                        }
                    }
                }

                return new PackageNameSearchResult(infos.Select(x => x.Id).ToList(), FeedKind.Local);
            }
            catch
            {
                return PackageNameSearchResult.Failure;
            }
        }, cancellationToken);
    }

    public Task<IPackageVersionSearchResult> GetPackageVersionsAsync(string prefix, IPackageQueryConfiguration queryConfiguration, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            try
            {
                List<string> versions = new List<string>();
                bool anyFound = false;
                foreach (string path in _fileSystem.EnumerateDirectories(_feed).Where(x => x.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) > -1))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return PackageVersionSearchResult.Cancelled;
                    }

                    string nuspec = _fileSystem.EnumerateFiles(path, "*.nuspec", SearchOption.TopDirectoryOnly).FirstOrDefault();

                    if (nuspec != null)
                    {
                        anyFound = true;
                        IPackageInfo info = NuSpecReader.Read(nuspec, FeedKind.Local);

                        if (info != null && string.Equals(info.Id, prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            versions.Add(info.Version);
                        }
                    }
                }

                if (anyFound)
                {
                    return new PackageVersionSearchResult(versions, FeedKind.Local);
                }

                return PackageVersionSearchResult.Failure;
            }
            catch
            {
                return PackageVersionSearchResult.Failure;
            }
        }, cancellationToken);
    }
}
