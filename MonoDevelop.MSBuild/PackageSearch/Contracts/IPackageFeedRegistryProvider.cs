// Copyright (c).NET Foundation and Contributors
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace ProjectFileTools.NuGetSearch.Contracts;

public interface IPackageFeedRegistryProvider
{
    IReadOnlyList<string> ConfiguredFeeds { get; }
}
