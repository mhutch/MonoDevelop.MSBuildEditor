// Copyright (c).NET Foundation and Contributors
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

using ProjectFileTools.NuGetSearch.Contracts;
using ProjectFileTools.NuGetSearch.IO;
using ProjectFileTools.NuGetSearch.Search;

namespace ProjectFileTools.NuGetSearch.Feeds.Web;

public class NuGetV2ServiceFeedFactory : IPackageFeedFactory
{
    private readonly IWebRequestFactory _webRequestFactory;

    public NuGetV2ServiceFeedFactory(IWebRequestFactory webRequestFactory)
    {
        _webRequestFactory = webRequestFactory;
    }

    public bool TryHandle(string feed, out IPackageFeed instance)
    {
        if (Uri.TryCreate(feed, UriKind.Absolute, out Uri location) && feed.EndsWith("nuget", StringComparison.OrdinalIgnoreCase))
        {
            instance = new NuGetV2ServiceFeed(feed, _webRequestFactory);
            return true;
        }

        instance = null;
        return false;
    }
}

public class NuGetV2ServiceFeed : IPackageFeed
{
    private readonly string _feed;
    private readonly FeedKind _kind;
    private readonly IWebRequestFactory _webRequestFactory;

    public NuGetV2ServiceFeed(string feed, IWebRequestFactory webRequestFactory)
    {
        _webRequestFactory = webRequestFactory;
        _feed = feed;
        _kind = feed.IndexOf("nuget.org", StringComparison.OrdinalIgnoreCase) > -1 ? FeedKind.NuGet : FeedKind.MyGet;
    }

    public string DisplayName => $"{_feed} (NuGet v2)";


    public async Task<IPackageNameSearchResult> GetPackageNamesAsync(string prefix, IPackageQueryConfiguration queryConfiguration, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return PackageNameSearchResult.Failure;
        }

        IReadOnlyList<string> results = new List<string>();
        string frameworkQuery = !string.IsNullOrEmpty(queryConfiguration.CompatibilityTarget) ? $"&targetFramework={queryConfiguration.CompatibilityTarget}" : "";
        var serviceEndpoint = $"{_feed}/Search()";
        Func<string, string> queryFunc = x => $"{x}?searchTerm='{prefix}'{frameworkQuery}&includePrerelease={queryConfiguration.IncludePreRelease}&semVerLevel=2.0.0";
        XDocument document = await ExecuteAutocompleteServiceQueryAsync(serviceEndpoint, queryFunc, cancellationToken).ConfigureAwait(false);

        if (document != null)
        {
            try
            {
                results = GetPackageNamesFromNuGetV2CompatibleQueryResult(document);
                return new PackageNameSearchResult(results, _kind);
            }
            catch
            {
            }
        }

        return PackageNameSearchResult.Failure;
    }

    public async Task<IPackageInfo> GetPackageInfoAsync(string packageId, string version, IPackageQueryConfiguration queryConfiguration, CancellationToken cancellationToken)
    {
        if (packageId == null)
        {
            return null;
        }

        var serviceEndpoint = $"{_feed}/Packages(Id='{packageId}',Version='{version}')";
        Func<string, string> queryFunc = x => $"{x}";
        XDocument document = await ExecuteAutocompleteServiceQueryAsync(serviceEndpoint, queryFunc, cancellationToken).ConfigureAwait(false);

        if (document != null)
        {
            var el = document.Root;

            var id = GetPropertyValue(document, el, "Id");
            var title = GetPropertyValue(document, el, "Title");
            var authors = GetPropertyValue(document, el, "Authors");
            var summary = GetPropertyValue(document, el, "Summary");
            var description = GetPropertyValue(document, el, "Description");
            var projectUrl = GetPropertyValue(document, el, "ProjectUrl");
            var licenseUrl = GetPropertyValue(document, el, "LicenseUrl");
            var iconUrl = GetPropertyValue(document, el, "IconUrl");
            var tags = GetPropertyValue(document, el, "Tags");
            var packageInfo = new PackageInfo(id, version, title, authors, summary, description, licenseUrl, projectUrl, iconUrl, tags, _kind, null);
            return packageInfo;
        }


        return null;
    }

    public async Task<IPackageVersionSearchResult> GetPackageVersionsAsync(string id, IPackageQueryConfiguration queryConfiguration, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> results = new List<string>();
        var serviceEndpoint = $"{_feed}/FindPackagesById()";
        Func<string, string> queryFunc = x => $"{x}?id='{id}'";
        XDocument document = await ExecuteAutocompleteServiceQueryAsync(serviceEndpoint, queryFunc, cancellationToken).ConfigureAwait(false);

        try
        {
            results = GetPackageVersionsFromNuGetV2CompatibleQueryResult(id, document);
        }
        catch
        {
            return PackageVersionSearchResult.Failure;
        }

        return new PackageVersionSearchResult(results, _kind);
    }

    private async Task<XDocument> ExecuteAutocompleteServiceQueryAsync(string endpoint, Func<string, string> query, CancellationToken cancellationToken)
    {
        if (endpoint == null)
        {
            return null;
        }

        try
        {
            string location = query(endpoint);
            var xml = await _webRequestFactory.GetStringAsync(location, cancellationToken).ConfigureAwait(false);
            return XDocument.Parse(xml);
        }
        catch (Exception)
        {
        }

        return null;
    }

        
    private static string GetPropertyValue(XDocument document, XElement el, string propertyKey)
    {
        return el
                .Element(document.Root.GetNamespaceOfPrefix("m") + "properties")
                .Element(document.Root.GetNamespaceOfPrefix("d") + propertyKey)
                ?.Value;
    }

    

    private static IReadOnlyList<string> GetPackageVersionsFromNuGetV2CompatibleQueryResult(string id, XDocument document)
    {
        List<string> results = new List<string>();

        if (document != null)
        {
            foreach (var el in document.Root.Elements(document.Root.GetDefaultNamespace() + "entry"))
            {
                var pkgId = GetPropertyValue(document, el, "Id");

                var pkgVersion = GetPropertyValue(document, el, "Version");

                if (pkgId.Equals(pkgId, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(pkgVersion);
                }
            }
        }

        return results;
    }

    private static IReadOnlyList<string> GetPackageNamesFromNuGetV2CompatibleQueryResult(XDocument document)
    {
        List<string> results = new List<string>();

        if (document != null)
        {
            foreach (var el in document.Root.Elements(document.Root.GetDefaultNamespace() + "entry"))
            {
                var id = GetPropertyValue(document, el, "Id");

                results.Add(id);
            }
        }

        return results.Distinct().ToList();
    }
}
