// Copyright (c).NET Foundation and Contributors
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using ProjectFileTools.NuGetSearch.Contracts;

namespace ProjectFileTools.NuGetSearch.Feeds;

public class PackageQueryConfiguration : IPackageQueryConfiguration
{
    public PackageQueryConfiguration(string? targetFrameworkMoniker, bool includePreRelease = true, int maxResults = 100, PackageType? packageType = null)
    {
        CompatibilityTarget = targetFrameworkMoniker;
        IncludePreRelease = includePreRelease;
        MaxResults = maxResults;
        PackageType = packageType;
    }

    public string? CompatibilityTarget { get; }

    public bool IncludePreRelease { get; }

    public int MaxResults { get; }

    public PackageType? PackageType { get; }

    public override int GetHashCode()
    {
        return (CompatibilityTarget?.GetHashCode() ?? 0) ^ IncludePreRelease.GetHashCode() ^ MaxResults.GetHashCode() ^ (PackageType?.GetHashCode() ?? 0);
    }

    public override bool Equals(object? obj)
    {
        return obj is PackageQueryConfiguration cfg
            && string.Equals(CompatibilityTarget, cfg.CompatibilityTarget, System.StringComparison.Ordinal)
            && IncludePreRelease == cfg.IncludePreRelease
            && MaxResults == cfg.MaxResults
            && (PackageType?.Equals(cfg.PackageType) ?? false);
    }
}