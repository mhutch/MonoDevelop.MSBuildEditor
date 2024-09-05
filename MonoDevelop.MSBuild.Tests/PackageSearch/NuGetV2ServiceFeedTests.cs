// Copyright (c).NET Foundation and Contributors
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;

using Moq;

using NUnit.Framework;

using ProjectFileTools.NuGetSearch.Feeds;
using ProjectFileTools.NuGetSearch.Feeds.Web;
using ProjectFileTools.NuGetSearch.IO;

namespace ProjectFileTools.NuGetSearch.Tests;

/// <summary>
/// The tests below only work when signing is disabled.
/// When signing enabled, no test will be found as a result of `ProjectFileTools.NuGetSearch` failing to load with signing key validation error
/// </summary>
[TestFixture]
public class NuGetV2ServiceFeedTests
{

    [Theory]
    [TestCase("http://localhost/nuget")]
    public void GivenFeed_ReturnDisplayName(string feed)
    {
        var webRequestFactory = Mock.Of<IWebRequestFactory>();
        var sut = new NuGetV2ServiceFeed(feed, webRequestFactory);
        sut.DisplayName.Should().Be($"{feed} (NuGet v2)");
    }

    [Theory]
    [TestCase("http://localhost/nuget", "GetPackageNames.CommonLogging.xml")]
    public async Task GivenPackagesFound_ReturnListOfIds(string feed, string testFile)
    {
        var webRequestFactory = Mock.Of<IWebRequestFactory>();

        Mock.Get(webRequestFactory)
            .Setup(f => f.GetStringAsync("http://localhost/nuget/Search()?searchTerm='Common.Logging'&targetFramework=netcoreapp2.0&includePrerelease=False&semVerLevel=2.0.0", It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(GetXmlFromTestFile(testFile)));

        var sut = new NuGetV2ServiceFeed(feed, webRequestFactory);

        var packageNameResults = await sut.GetPackageNamesAsync("Common.Logging", new PackageQueryConfiguration("netcoreapp2.0", false), new CancellationToken());
        packageNameResults.Names.Count.Should().Be(5);
    }

    [Theory]
    [TestCase("http://localhost/nuget", "GetPackageVersions.CommonLogging.xml")]
    public async Task GivenPackagesFound_ReturnListOfVersions(string feed, string testFile)
    {
        var webRequestFactory = Mock.Of<IWebRequestFactory>();

        Mock.Get(webRequestFactory)
            .Setup(f => f.GetStringAsync("http://localhost/nuget/FindPackagesById()?id='Acme.Common.Logging.AspNetCore'", It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(GetXmlFromTestFile(testFile)));

        var sut = new NuGetV2ServiceFeed(feed, webRequestFactory);

        var packageNameResults = await sut.GetPackageVersionsAsync("Acme.Common.Logging.AspNetCore", new PackageQueryConfiguration("netcoreapp2.0", false), new CancellationToken());
        packageNameResults.Versions.Count.Should().Be(8);
        Assert.That(packageNameResults.Versions, Is.EqualTo (new[] {
            "1.6.0.5",
            "1.6.1",
            "1.6.2",
            "1.7.0",
            "1.7.1",
            "1.8.0",
            "1.9.0",
            "1.9.1"
        }));
    }

    [Theory]
    [TestCase("http://localhost/nuget", "GetPackageInfo.CommonLogging.xml")]
    public async Task GivenPackageFound_ReturnPackageInfo(string feed, string testFile)
    {
        var webRequestFactory = Mock.Of<IWebRequestFactory>();

        Mock.Get(webRequestFactory)
            .Setup(f => f.GetStringAsync("http://localhost/nuget/Packages(Id='Acme.Common.Logging.AspNetCore',Version='1.8.0')", It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(GetXmlFromTestFile(testFile)));

        var sut = new NuGetV2ServiceFeed(feed, webRequestFactory);

        var pkgInfo = await sut.GetPackageInfoAsync("Acme.Common.Logging.AspNetCore", "1.8.0", new PackageQueryConfiguration("netcoreapp2.0", false), new CancellationToken());

        pkgInfo.Id.Should().Be("Acme.Common.Logging.AspNetCore");
        pkgInfo.Title.Should().Be("Common Logging AspNetCore");
        pkgInfo.Summary.Should().BeNullOrEmpty();
        pkgInfo.Description.Should().Be("Common Logging integration within Aspnet core services");
        pkgInfo.Authors.Should().Be("Patrick Assuied");
        pkgInfo.Version.Should().Be("1.8.0");
        pkgInfo.ProjectUrl.Should().Be("https://bitbucket.acme.com/projects/Acme/repos/Acme-common-logging");
        pkgInfo.LicenseUrl.Should().BeNullOrEmpty();
        pkgInfo.Tags.Should().Be(" common logging aspnetcore ");
    }

    public static string GetXmlFromTestFile(string filename, [CallerFilePath] string callerFile = null)
    {
        string path = Path.Combine(Path.GetDirectoryName(callerFile), "TestFiles", filename);
        return File.ReadAllText(path);
    }
}
