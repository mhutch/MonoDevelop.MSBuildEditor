// Copyright (c).NET Foundation and Contributors
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace ProjectFileTools.NuGetSearch.IO;

public static class WebRequestFactoryExtensions
{
    public static async Task<JToken> GetJsonAsync(this IWebRequestFactory factory, string endpoint, CancellationToken cancellationToken)
    {
        string result = await factory.GetStringAsync(endpoint, cancellationToken).ConfigureAwait(false);

        if(result == null)
        {
            return null;
        }

        try
        {
            return JToken.Parse(result);
        }
        catch
        {
            return null;
        }
    }

    public static async Task<T> GetDeserializedJsonAsync<T>(this IWebRequestFactory factory, string endpoint, CancellationToken cancellationToken)
    {
        JToken token = await factory.GetJsonAsync(endpoint, cancellationToken).ConfigureAwait(false);

        if(token == null)
        {
            return default(T);
        }

        return token.ToObject<T>();
    }
}
