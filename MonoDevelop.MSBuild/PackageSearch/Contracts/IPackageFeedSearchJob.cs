// Copyright (c).NET Foundation and Contributors
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace ProjectFileTools.NuGetSearch.Contracts;

public interface IPackageFeedSearchJob<T>
{
    IReadOnlyList<string> RemainingFeeds { get; }

    IReadOnlyList<string> SearchingIn { get; }

    IReadOnlyList<T> Results { get; }

    bool IsCancelled { get; }

    event EventHandler Updated;

    void Cancel();
}
