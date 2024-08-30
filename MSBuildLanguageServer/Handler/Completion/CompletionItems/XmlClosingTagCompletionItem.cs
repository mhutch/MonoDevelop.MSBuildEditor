// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.Xml.Dom;

using Roslyn.LanguageServer.Protocol;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler.Completion.CompletionItems;

class XmlClosingTagCompletionItem(bool includeBracket, string name, XElement element, int dedupCount) : ILspCompletionItem
{
    readonly string label = (includeBracket ? "</" : "/") + name;

    public bool IsMatch(CompletionItem request) => string.Equals(request.Label, label, StringComparison.Ordinal);

    // TODO: custom insert text, including for multiple closing tags
    public ValueTask<CompletionItem> Render(CompletionRenderSettings settings, CancellationToken cancellationToken)
    {
        // force these to sort last, they're not very interesting values to browse as these tags are usually already closed
        string sortText = "ZZZZZZ" + label;

        var item = new CompletionItem { Label = label, Kind = XmlToLspCompletionItemKind.ClosingTag, SortText = sortText };

        if(settings.IncludeDocumentation)
        {
            item.Documentation = GetClosingTagDocumentation(element, dedupCount > 1);
        };

        return new(item);
    }


    static MarkupContent GetClosingTagDocumentation(XElement element, bool isMultiple)
        => CreateMarkdown(
            isMultiple
                ? $"Closing tag for element `{element.Name}`, closing all intermediate elements"
                : $"Closing tag for element `{element.Name}`"
            );

    static MarkupContent CreateMarkdown(string markdown) => new() { Kind = MarkupKind.Markdown, Value = markdown };
}
