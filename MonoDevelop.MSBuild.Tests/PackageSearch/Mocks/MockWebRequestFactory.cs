// Copyright (c).NET Foundation and Contributors
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using ProjectFileTools.NuGetSearch.IO;

namespace ProjectFileTools.NuGetSearch.Tests;

public class MockWebRequestFactory : IWebRequestFactory
{
    public Task<string> GetStringAsync(string endpoint, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
