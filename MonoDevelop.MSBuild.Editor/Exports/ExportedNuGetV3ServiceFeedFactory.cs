// Copyright (c).NET Foundation and Contributors
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;
using ProjectFileTools.NuGetSearch.Contracts;
using ProjectFileTools.NuGetSearch.Feeds.Web;
using ProjectFileTools.NuGetSearch.IO;

namespace ProjectFileTools.Exports;

[Export(typeof(IPackageFeedFactory))]
[Name("Default NuGet v3 Service Feed Factory")]
internal class ExportedNuGetV3ServiceFeedFactory : NuGetV3ServiceFeedFactory
{
    [ImportingConstructor]
    public ExportedNuGetV3ServiceFeedFactory(IWebRequestFactory webRequestFactory)
        : base(webRequestFactory)
    {
    }
}
