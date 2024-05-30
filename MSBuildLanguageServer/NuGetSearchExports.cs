// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Composition;
using Microsoft.CodeAnalysis.LanguageServer.Handler;

using Microsoft.CodeAnalysis.LanguageServer;

using ProjectFileTools.NuGetSearch.Contracts;
using ProjectFileTools.NuGetSearch.Feeds;
using ProjectFileTools.NuGetSearch.Feeds.Disk;
using ProjectFileTools.NuGetSearch.Feeds.Web;
using ProjectFileTools.NuGetSearch.IO;
using ProjectFileTools.NuGetSearch.Search;

namespace MonoDevelop.MSBuild.Editor.NuGetSearch;

[ExportCSharpVisualBasicLspServiceFactory(typeof(NuGetSearchService)), Shared]
[method: ImportingConstructor]
class NuGetSearchServiceFactory(IPackageSearchManager packageSearchManager) : ILspServiceFactory
{
    public ILspService CreateILspService(LspServices lspServices, WellKnownLspServerKinds serverKind) => new NuGetSearchService (packageSearchManager);

}

class NuGetSearchService(IPackageSearchManager packageSearchManager) : ILspService, IPackageSearchManager
{
    public IPackageFeedSearchJob<IPackageInfo> SearchPackageInfo(string packageId, string? version, string? tfm) => packageSearchManager.SearchPackageInfo(packageId, version, tfm);
    public IPackageFeedSearchJob<Tuple<string, FeedKind>> SearchPackageNames(string prefix, string? tfm, string? packageType = null) => packageSearchManager.SearchPackageNames(prefix, tfm, packageType);
    public IPackageFeedSearchJob<Tuple<string, FeedKind>> SearchPackageVersions(string packageName, string? tfm, string? packageType = null) => packageSearchManager.SearchPackageVersions(packageName, tfm, packageType);
}

[Export(typeof(IPackageFeedFactorySelector))]
[method: ImportingConstructor]
internal class ExportedPackageFeedFactorySelector([ImportMany] IEnumerable<IPackageFeedFactory> feedFactories) : PackageFeedFactorySelector(feedFactories)
{
}

[Export(typeof(IFileSystem))]
internal class ExportedFileSystem : FileSystem
{
}

[Export(typeof(IPackageFeedFactory))]
[method: ImportingConstructor]
internal class ExportedNuGetDiskFeedFactory(IFileSystem fileSystem) : NuGetDiskFeedFactory(fileSystem)
{
}

[Export(typeof(IPackageFeedFactory))]
[method: ImportingConstructor]
internal class ExportedNuGetV3ServiceFeedFactory(IWebRequestFactory webRequestFactory) : NuGetV3ServiceFeedFactory(webRequestFactory)
{
}

[Export(typeof(IPackageSearchManager))]
[method: ImportingConstructor]
internal class ExportedPackageSearchManager(IPackageFeedRegistryProvider feedRegistry, IPackageFeedFactorySelector factorySelector) : PackageSearchManager(feedRegistry, factorySelector)
{
}

[Export(typeof(IWebRequestFactory))]
internal class ExportedWebRequestFactory : WebRequestFactory
{
}

[Export(typeof(IPackageFeedRegistryProvider))]
internal class ExportedPackageFeedRegistryProvider : IPackageFeedRegistryProvider
{
    // TODO: where should we get this from? hardcode some basic ones for now
    public IReadOnlyList<string> ConfiguredFeeds { get; } = [
        "https://api.nuget.org/v3/index.json",
        Environment.ExpandEnvironmentVariables("%USERPROFILE%\\.nuget\\packages")
    ];
}