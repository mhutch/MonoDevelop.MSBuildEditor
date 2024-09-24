// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler.CodeActions;

class CodeActionResolveData
{
    [JsonPropertyName("resultId")]
    [JsonRequired]
    public long ResultId { get; init; }

    [JsonPropertyName("index")]
    [JsonRequired]
    public int Index { get; init; }
}
