// Copyright (c).NET Foundation and Contributors
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Xml.Linq;
using ProjectFileTools.NuGetSearch.Contracts;

namespace ProjectFileTools.NuGetSearch.Feeds;

internal class NuSpecReader
{
    internal static IPackageInfo Read(string nuspec, FeedKind kind)
    {
        XDocument doc = XDocument.Load(nuspec);
        XNamespace ns = doc.Root.GetDefaultNamespace();
        XElement package = doc.Root;
        XElement metadata = package?.Element(XName.Get("metadata", ns.NamespaceName));
        XElement id = metadata?.Element(XName.Get("id", ns.NamespaceName));
        XElement version = metadata?.Element(XName.Get("version", ns.NamespaceName));
        XElement title = metadata?.Element(XName.Get("title", ns.NamespaceName));
        XElement authors = metadata?.Element(XName.Get("authors", ns.NamespaceName));
        XElement summary = metadata?.Element (XName.Get ("summary", ns.NamespaceName));
        XElement description = metadata?.Element(XName.Get("description", ns.NamespaceName));
        XElement licenseUrl = metadata?.Element(XName.Get("licenseUrl", ns.NamespaceName));
        XElement projectUrl = metadata?.Element (XName.Get ("projectUrl", ns.NamespaceName));
        XElement iconUrl = metadata?.Element (XName.Get ("iconUrl", ns.NamespaceName));
        XElement tags = metadata?.Element(XName.Get("tags", ns.NamespaceName));

        List<PackageType> packageTypes = null;
        XElement packageTypesEl = metadata?.Element(XName.Get("packageTypes", ns.NamespaceName));
        if (packageTypesEl != null)
        {
            var nameName = XName.Get("name");
            var versionName = XName.Get("version");
            packageTypes = new List<PackageType>();
            foreach (var packageType in packageTypesEl.Elements(XName.Get("packageType", ns.NamespaceName)))
            {
                var typeName = packageType.Attribute(nameName).Value;
                var typeVersion = packageType.Attribute(versionName)?.Value;
                packageTypes.Add (new PackageType(typeName, typeVersion));
            }
        }

        if (id != null)
        {
            return new PackageInfo(
                id.Value, version?.Value, title?.Value, authors?.Value, summary?.Value, description?.Value,
                licenseUrl?.Value, projectUrl?.Value, iconUrl?.Value, tags?.Value, kind, packageTypes);
        }

        return null;
    }
}
