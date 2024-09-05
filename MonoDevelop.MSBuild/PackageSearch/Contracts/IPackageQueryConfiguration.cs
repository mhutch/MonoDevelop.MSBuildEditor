// Copyright (c).NET Foundation and Contributors
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace ProjectFileTools.NuGetSearch.Contracts;

public interface IPackageQueryConfiguration
{
    string? CompatibilityTarget { get; }

    bool IncludePreRelease { get; }

    int MaxResults { get; }

    PackageType? PackageType { get; }
}
