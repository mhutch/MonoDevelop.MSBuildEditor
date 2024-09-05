// Copyright (c).NET Foundation and Contributors
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using ProjectFileTools.NuGetSearch.Contracts;
using ProjectFileTools.NuGetSearch.IO;
using ProjectFileTools.NuGetSearch.Search;

namespace ProjectFileTools.NuGetSearch.Feeds.Web;

public class NuGetV3ServiceFeedFactory : IPackageFeedFactory
{
    private readonly IWebRequestFactory _webRequestFactory;

    public NuGetV3ServiceFeedFactory(IWebRequestFactory webRequestFactory)
    {
        _webRequestFactory = webRequestFactory;
    }

    public bool TryHandle(string feed, out IPackageFeed instance)
    {
        if (Uri.TryCreate(feed, UriKind.Absolute, out Uri location) && feed.EndsWith("v3/index.json", StringComparison.OrdinalIgnoreCase))
        {
            instance = new NuGetV3ServiceFeed(feed, _webRequestFactory);
            return true;
        }

        instance = null;
        return false;
    }
}

public class NuGetV3ServiceFeed : IPackageFeed
{
    private readonly string _feed;
    private readonly FeedKind _kind;
    private readonly IWebRequestFactory _webRequestFactory;

    public NuGetV3ServiceFeed(string feed, IWebRequestFactory webRequestFactory)
    {
        _webRequestFactory = webRequestFactory;
        _feed = feed;
        _kind = feed.IndexOf("nuget.org", StringComparison.OrdinalIgnoreCase) > -1 ? FeedKind.NuGet : FeedKind.MyGet;
    }

    public string DisplayName => $"{_feed} (NuGet v3)";

    private async Task<JObject> ExecuteAutocompleteServiceQueryAsync(List<string> endpoints, Func<string, string> query, CancellationToken cancellationToken)
    {
        if (endpoints == null)
        {
            return null;
        }

        for (int i = 0; i < endpoints.Count; ++i)
        {
            string endpoint = endpoints[0];

            try
            {
                string location = query(endpoint);
                return await _webRequestFactory.GetJsonAsync(location, cancellationToken).ConfigureAwait(false) as JObject;
            }
            catch (Exception)
            {
                if (endpoints.Count > 1)
                {
                    endpoints.RemoveAt(0);
                    endpoints.Add(endpoint);
                }

                //TODO: Possibly log the failure to get the document 
            }
        }

        return null;
    }

    private async Task<List<string>> DiscoverEndpointsAsync(string packageSource, string autoCompleteServiceTypeIdentifier, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(packageSource))
        {
            return null;
        }

        string requestUrl = packageSource.TrimEnd('/');

        //If we're already requesting /vN/index.json, use it. Otherwise, try to fix up the url to discover an 
        //  index.json file to get endpoints from
        if (!requestUrl.EndsWith("/index.json", StringComparison.OrdinalIgnoreCase))
        {
            string baseUrl = packageSource;
            if (packageSource[packageSource.Length - 1] != '/')
            {
                baseUrl += "/";
            }

            if (baseUrl.Length < 2)
            {
                return null;
            }

            int lastSlashIndex = baseUrl.LastIndexOf('/', baseUrl.Length - 2);

            //If the only slash in the value we're processing is the one we just added,
            //  there's something wrong, quit
            if (lastSlashIndex < 0)
            {
                return null;
            }

            string lastSegment = baseUrl.Substring(lastSlashIndex + 1);

            //If the base url ended in "v2", move to "v3"
            if (string.Equals(lastSegment, "v2/", StringComparison.OrdinalIgnoreCase))
            {
                baseUrl = baseUrl.Substring(0, lastSlashIndex + 1) + "v3/";
            }
            //If the base url didn't include an understood version identifier, add v3
            else if (!string.Equals(lastSegment, "v3/", StringComparison.OrdinalIgnoreCase))
            {
                baseUrl += "v3/";
            }

            requestUrl = baseUrl + "index.json";
        }

        try
        {
            JObject responseJson = await _webRequestFactory.GetJsonAsync(requestUrl, cancellationToken).ConfigureAwait(false) as JObject;
            return FindEndpointsInIndexJSON(responseJson, autoCompleteServiceTypeIdentifier);
        }
        catch
        {
            return null;
        }
    }

    private static List<string> FindEndpointsInIndexJSON(JObject document, string serviceTypeIdentifier)
    {
        List<string> endpoints = new List<string>();

        JArray resourcesArray = document?["resources"] as JArray;

        if (resourcesArray == null)
        {
            return endpoints;
        }

        foreach (JToken curResource in resourcesArray)
        {
            JObject curObject = curResource as JObject;
            JArray typeArray = curObject?["@type"] as JArray;
            bool foundMatchingService = false;

            if (typeArray != null)
            {
                foreach (JToken curToken in typeArray)
                {
                    if (string.Equals(curToken.ToString(), serviceTypeIdentifier, StringComparison.Ordinal))
                    {
                        foundMatchingService = true;
                        break;
                    }
                }
            }
            else if (curObject != null)
            {
                // NuGet team indicated a desired to handle fallback scenario where this is a simple string also
                if (string.Equals(curObject["@type"].ToString(), serviceTypeIdentifier, StringComparison.Ordinal))
                {
                    foundMatchingService = true;
                }
            }

            if (curObject != null && foundMatchingService)
            {
                endpoints.Add(curObject["@id"].ToString());
            }
        }

        return endpoints;
    }

    public async Task<IPackageNameSearchResult> GetPackageNamesAsync(string prefix, IPackageQueryConfiguration queryConfiguration, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return PackageNameSearchResult.Failure;
        }

        IReadOnlyList<string> results = new List<string>();
        string frameworkQuery = !string.IsNullOrEmpty(queryConfiguration.CompatibilityTarget) ? $"&supportedFramework={queryConfiguration.CompatibilityTarget}" : "";
        string packageTypeQuery = !string.IsNullOrEmpty(queryConfiguration.PackageType?.Name) ? $"&packageType={queryConfiguration.PackageType.Name}" : "";
        const string autoCompleteServiceTypeIdentifier = "SearchAutocompleteService/3.5.0";
        List<string> serviceEndpoints = await DiscoverEndpointsAsync(_feed, autoCompleteServiceTypeIdentifier, cancellationToken).ConfigureAwait(false);
        Func<string, string> queryFunc = x => $"{x}?q={prefix}&semVerLevel=2.0.0{frameworkQuery}{packageTypeQuery}&take={queryConfiguration.MaxResults}&prerelease={queryConfiguration.IncludePreRelease}";
        JObject document = await ExecuteAutocompleteServiceQueryAsync(serviceEndpoints, queryFunc, cancellationToken).ConfigureAwait(false);

        if (document != null)
        {
            try
            {
                results = GetDataFromNuGetV3CompatibleQueryResult(document);
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

        string packageDisplayMetadataUriTemplateIdentifier = "PackageDisplayMetadataUriTemplate/3.0.0-rc";
        List<string> packageQuickInfoAddresses = await DiscoverEndpointsAsync(_feed, packageDisplayMetadataUriTemplateIdentifier, cancellationToken).ConfigureAwait(false);

        if (packageQuickInfoAddresses == null || packageQuickInfoAddresses.Count == 0)
        {
            return null;
        }

        string packageQuickInfoAddress = packageQuickInfoAddresses[0];

        string location = packageQuickInfoAddress.Replace("{id-lower}", packageId.ToLowerInvariant());
        JObject responseJson;

        try
        {
            responseJson = await _webRequestFactory.GetJsonAsync(location, cancellationToken).ConfigureAwait(false) as JObject;
        }
        catch
        {
            return null;
        }

        if (responseJson != null && responseJson.TryGetValue("items", out JToken topLevelItemsParseItem))
        {
            JArray topLevelItemsArray = topLevelItemsParseItem as JArray;
            JObject packageResultsContainer = topLevelItemsArray?.FirstOrDefault() as JObject;
            JToken packageResultsItemsParseItem;

            if (packageResultsContainer != null && packageResultsContainer.TryGetValue("items", out packageResultsItemsParseItem))
            {
                JArray packageResultsItems = packageResultsItemsParseItem as JArray;

                if (packageResultsItemsParseItem == null)
                {
                    return null;
                }

                string id, title, authors, summary, description, licenseUrl, projectUrl, iconUrl, tags;
                SemanticVersion bestSemanticVersion = null;
                PackageInfo packageInfo = null;

                foreach (JToken element in packageResultsItems)
                {
                    JObject packageContainer = element as JObject;
                    JToken catalogEntryParseItem;

                    if (packageContainer != null && packageContainer.TryGetValue("catalogEntry", out catalogEntryParseItem))
                    {
                        JObject catalogEntry = catalogEntryParseItem as JObject;

                        if (catalogEntry != null)
                        {
                            string ver = catalogEntry["version"]?.ToString();

                            if (ver == null)
                            {
                                continue;
                            }

                            SemanticVersion currentVersion = SemanticVersion.Parse(ver);

                            if (version != null)
                            {
                                if (string.Equals(version, catalogEntry["version"]?.ToString(), StringComparison.OrdinalIgnoreCase))
                                {
                                    id = catalogEntry["id"]?.ToString();
                                    title = catalogEntry ["title"]?.ToString ();
                                    authors = catalogEntry["authors"]?.ToString();
                                    summary = catalogEntry ["summary"]?.ToString ();
                                    description = catalogEntry["description"]?.ToString();
                                    projectUrl = catalogEntry["projectUrl"]?.ToString();
                                    licenseUrl = catalogEntry["licenseUrl"]?.ToString();
                                    iconUrl = catalogEntry["iconUrl"]?.ToString();
                                    tags = catalogEntry ["tags"]?.ToString ();
                                    packageInfo = new PackageInfo(id, version, title, authors, summary, description, licenseUrl, projectUrl, iconUrl, tags, _kind, null);
                                    return packageInfo;
                                }
                            }
                            else
                            {
                                if(currentVersion.CompareTo(bestSemanticVersion) > 0)
                                {
                                    id = catalogEntry["id"]?.ToString();
                                    title = catalogEntry["title"]?.ToString ();
                                    authors = catalogEntry["authors"]?.ToString();
                                    summary = catalogEntry["summary"]?.ToString();
                                    description = catalogEntry["description"]?.ToString ();
                                    projectUrl = catalogEntry["projectUrl"]?.ToString();
                                    licenseUrl = catalogEntry["licenseUrl"]?.ToString();
                                    iconUrl = catalogEntry["iconUrl"]?.ToString ();
                                    tags = catalogEntry["tags"]?.ToString();
                                    packageInfo = new PackageInfo(id, version, title, authors, summary, description, licenseUrl, projectUrl, iconUrl, tags, _kind, null);
                                    bestSemanticVersion = currentVersion;
                                }
                            }
                        }
                    }
                }

                return packageInfo;
            }
        }

        return null;
    }

    public async Task<IPackageVersionSearchResult> GetPackageVersionsAsync(string id, IPackageQueryConfiguration queryConfiguration, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> results = new List<string>();
        string frameworkQuery = !string.IsNullOrEmpty(queryConfiguration.CompatibilityTarget) ? $"&supportedFramework={queryConfiguration.CompatibilityTarget}" : "";
        const string autoCompleteServiceTypeIdentifier = "SearchAutocompleteService";
        List<string> serviceEndpoints = await DiscoverEndpointsAsync(_feed, autoCompleteServiceTypeIdentifier, cancellationToken).ConfigureAwait(false);
        Func<string, string> queryFunc = x => $"{x}?id={id}{frameworkQuery}&take={queryConfiguration.MaxResults}&prerelease={queryConfiguration.IncludePreRelease}";
        JObject document = await ExecuteAutocompleteServiceQueryAsync(serviceEndpoints, queryFunc, cancellationToken).ConfigureAwait(false);

        try
        {
            results = GetDataFromNuGetV3CompatibleQueryResult(document);
        }
        catch
        {
            return PackageVersionSearchResult.Failure;
        }

        return new PackageVersionSearchResult(results, _kind);
    }

    private static IReadOnlyList<string> GetDataFromNuGetV3CompatibleQueryResult(JObject document)
    {
        List<string> results = new List<string>();

        if (document != null)
        {
            JArray resultsArray = document["data"] as JArray;

            if (resultsArray != null)
            {
                foreach (JToken curResult in resultsArray)
                {
                    string curPackageId = curResult.ToString();
                    results.Add(curPackageId);
                }
            }
        }

        return results.AsReadOnly();
    }
}
