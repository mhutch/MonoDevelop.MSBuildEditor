// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Roslyn.LanguageServer.Protocol;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler;

/// <summary>
/// An object that can be resolved to a completion item
/// </summary>
interface ILspCompletionItem
{
    /// <summary>
    /// Resolve this object to a <see cref="CompletionItem"/>
    /// </summary>
    /// <param name="settings">Provides information about which properties should be included</param>
    /// <returns>A populated <see cref="CompletionItem"/></returns>
    ValueTask<CompletionItem> Render(CompletionRenderSettings settings, CancellationToken cancellationToken);

    /// <summary>
    /// Whether this is a match for resolving the requested item.
    /// </summary>
    bool IsMatch(CompletionItem request);
}