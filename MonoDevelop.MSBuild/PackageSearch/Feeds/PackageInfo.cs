// Copyright (c).NET Foundation and Contributors
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

using ProjectFileTools.NuGetSearch.Contracts;

namespace ProjectFileTools.NuGetSearch.Feeds;

public class PackageInfo : IPackageInfo
{
    public PackageInfo(string id, string version, string title, string authors, string summary, string description, string licenseUrl, string projectUrl, string iconUrl, string tags, FeedKind sourceKind, IReadOnlyList<PackageType> packageTypes)
    {
        Id = id;
        Version = version;
        Title = title;
        Authors = authors;
        Description = description;
        LicenseUrl = licenseUrl;
        ProjectUrl = projectUrl;
        SourceKind = sourceKind;
        IconUrl = iconUrl;
        Tags = tags;
        PackageTypes = packageTypes ?? PackageType.DefaultList;
    }

    public string Id { get; }

    public string Version { get; }

    public string Title { get; }

    public string Authors { get; }

    public string Summary { get; }

    public string Description { get; }

    public string LicenseUrl { get; }

    public string ProjectUrl { get; }

    public string IconUrl { get; }

    public string Tags { get; }

    public FeedKind SourceKind { get; }

    public IReadOnlyList<PackageType> PackageTypes { get; }
}
