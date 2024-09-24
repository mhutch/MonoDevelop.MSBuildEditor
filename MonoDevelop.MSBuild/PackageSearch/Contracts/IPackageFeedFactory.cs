// Copyright (c).NET Foundation and Contributors
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace ProjectFileTools.NuGetSearch.Contracts;

public interface IPackageFeedFactory
{
    bool TryHandle(string feed, out IPackageFeed instance);
}
