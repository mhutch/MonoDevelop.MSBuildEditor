// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;
using MonoDevelop.MSBuild.Options;
using MonoDevelop.Xml.Options;
using LSP = Roslyn.LanguageServer.Protocol;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler.Completion;

/// <summary>
/// Information common to rendering many/all items that may be used when rendering the
/// items upfront or cached and provided later when the item is resolved.
/// </summary>
/// <param name="EditRange"></param>
record struct CompletionRenderContext(LSP.Range EditRange, SourceText PreTriggerSourceText, IOptionsReader Options)
{
    /// <summary>
    /// Provides pre-resolved information about the completion settings so that the cost of reading
    /// settings is fixed rather than scaling with the number of completions.
    /// </summary>
    public MSBuildCompletionOptionValues CompletionOptions {get; } = new MSBuildCompletionOptionValues(Options);
}
