// Copyright (c).NET Foundation and Contributors
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;
using ProjectFileTools.NuGetSearch.Contracts;
using ProjectFileTools.NuGetSearch.Feeds;

namespace ProjectFileTools.Exports;

[Export(typeof(IPackageFeedFactorySelector))]
[Name("Default Package Feed Factory Selector")]
internal class ExportedPackageFeedFactorySelector : PackageFeedFactorySelector
{
    [ImportingConstructor]
    public ExportedPackageFeedFactorySelector([ImportMany] IEnumerable<IPackageFeedFactory> feedFactories)
        : base(feedFactories)
    {
    }
}