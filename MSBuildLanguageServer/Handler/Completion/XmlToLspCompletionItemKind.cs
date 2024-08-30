// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Roslyn.LanguageServer.Protocol;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler.Completion;

/// <summary>
/// Central location for mapping XML item kinds to <see cref="CompletionItemKind"/> values
/// </summary>
class XmlToLspCompletionItemKind
{
    public const CompletionItemKind ClosingTag = CompletionItemKind.CloseElement;
    public const CompletionItemKind Comment = CompletionItemKind.TagHelper;
    public const CompletionItemKind CData = CompletionItemKind.TagHelper;
    public const CompletionItemKind Prolog = CompletionItemKind.TagHelper;
    public const CompletionItemKind Entity = CompletionItemKind.TagHelper;
}
