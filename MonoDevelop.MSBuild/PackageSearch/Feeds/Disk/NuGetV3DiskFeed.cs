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

internal class NuGetV3DiskFeed : IPackageFeed
{
    private readonly string _feed;
    private readonly IFileSystem _fileSystem;
    private readonly bool _isRemote;

    public NuGetV3DiskFeed(string feed, IFileSystem fileSystem)
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
                return $"NuGet v3 (Remote: {_feed})";
            }

            return $"NuGet v3 (Local: {_feed})";
        }
    }

    public Task<IPackageInfo> GetPackageInfoAsync(string id, string version, IPackageQueryConfiguration queryConfiguration, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            if (version != null)
            {
                string nuspec = Path.Combine(_feed, id, version, $"{id}.nuspec");

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
                string package = Path.Combine(_feed, id);
                string dir = _fileSystem.EnumerateDirectories(package).OrderByDescending(x => SemanticVersion.Parse(_fileSystem.GetDirectoryName(x))).FirstOrDefault();

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
                foreach (string path in _fileSystem.EnumerateDirectories(_feed, $"*{prefix}*", SearchOption.TopDirectoryOnly).Where(x => x.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) > -1))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return null;
                    }

                    if (infos.Count >= queryConfiguration.MaxResults)
                    {
                        break;
                    }

                    foreach (string verDir in _fileSystem.EnumerateDirectories(path, "*", SearchOption.TopDirectoryOnly))
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return null;
                        }

                        if (verDir == null || SemanticVersion.Parse(verDir) == null)
                        {
                            continue;
                        }

                        string nuspec = _fileSystem.EnumerateFiles(verDir, "*.nuspec", SearchOption.TopDirectoryOnly).FirstOrDefault();

                        if (nuspec != null)
                        {
                            IPackageInfo info = NuSpecReader.Read(nuspec, FeedKind.Local);
                            if (info != null && NuGetPackageMatcher.IsMatch(verDir, info, queryConfiguration, _fileSystem))
                            {
                                infos.Add(info);

                                if (infos.Count >= queryConfiguration.MaxResults)
                                {
                                    break;
                                }
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

    public Task<IPackageVersionSearchResult> GetPackageVersionsAsync(string id, IPackageQueryConfiguration queryConfiguration, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            string packagePath = Path.Combine(_feed, id.ToLowerInvariant());
            if (!_fileSystem.DirectoryExists(packagePath))
            {
                return PackageVersionSearchResult.Failure;
            }

            try
            {
                List<string> versions = new List<string>();

                foreach (string directory in _fileSystem.EnumerateDirectories(packagePath, "*", SearchOption.TopDirectoryOnly))
                {
                    string version = SemanticVersion.Parse(_fileSystem.GetDirectoryNameOnly(directory))?.ToString();

                    if (version != null)
                    {
                        versions.Add(version);
                    }
                }

                return new PackageVersionSearchResult(versions, FeedKind.Local);
            }
            catch
            {
                return PackageVersionSearchResult.Failure;
            }
        }, cancellationToken);
    }
}
