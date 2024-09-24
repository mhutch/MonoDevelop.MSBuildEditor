// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

using Roslyn.LanguageServer.Protocol;
using LSP = Roslyn.LanguageServer.Protocol;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler.Completion;

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
    ValueTask<CompletionItem> Render(CompletionRenderSettings settings, CompletionRenderContext ctx, CancellationToken cancellationToken);

    /// <summary>
    /// Whether this is a match for resolving the requested item.
    /// </summary>
    bool IsMatch(CompletionItem request);
}

/// <summary>
/// Information common to rendering many/all items that may be used when rendering the
/// items upfront or cached and provided later when the item is resolved.
/// </summary>
/// <param name="EditRange"></param>
record struct CompletionRenderContext(LSP.Range EditRange, SourceText PreTriggerSourceText);
