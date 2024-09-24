// Copyright (c).NET Foundation and Contributors
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;

namespace ProjectFileTools.NuGetSearch.IO;

public interface IWebRequestFactory
{
    Task<string> GetStringAsync(string endpoint, CancellationToken cancellationToken);
}
