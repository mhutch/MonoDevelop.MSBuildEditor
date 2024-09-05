// Copyright (c).NET Foundation and Contributors
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;
using ProjectFileTools.NuGetSearch.Contracts;
using ProjectFileTools.NuGetSearch.Feeds.Disk;
using ProjectFileTools.NuGetSearch.IO;

namespace ProjectFileTools.Exports;

[Export(typeof(IPackageFeedFactory))]
[Name("Default Package Feed Factory")]
internal class ExportedNuGetDiskFeedFactory : NuGetDiskFeedFactory
{
    [ImportingConstructor]
    public ExportedNuGetDiskFeedFactory(IFileSystem fileSystem)
        : base(fileSystem)
    {
    }
}
