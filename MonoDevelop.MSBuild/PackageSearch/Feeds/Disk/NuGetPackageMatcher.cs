// Copyright (c).NET Foundation and Contributors
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;

using ProjectFileTools.NuGetSearch.Contracts;
using ProjectFileTools.NuGetSearch.IO;

namespace ProjectFileTools.NuGetSearch.Feeds.Disk;

internal static class NuGetPackageMatcher
{
    public static bool IsMatch(string dir, IPackageInfo info, IPackageQueryConfiguration queryConfiguration, IFileSystem fileSystem)
    {
        if (!queryConfiguration.IncludePreRelease)
        {
            SemanticVersion ver = SemanticVersion.Parse(info.Version);

            if(!string.IsNullOrEmpty(ver?.PrereleaseVersion))
            {
                return false;
            }
        }

        if (queryConfiguration.PackageType != null)
        {
            //NOTE: can't find any info on how the version is supposed to be handled (or what it's even for), so use an exact match
            if (!info.PackageTypes.Any(p => queryConfiguration.PackageType.Equals(p)))
            {
                return false;
            }
        }

        return true;
    }
}
