// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Roslyn.LanguageServer.Protocol;
using LSP = Roslyn.LanguageServer.Protocol;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler.Completion.CompletionItems;

class XmlCompletionItem(string label, CompletionItemKind kind, string markdownDocumentation, XmlCommitKind commitKind) : ILspCompletionItem
{
    public bool IsMatch(CompletionItem request) => string.Equals(request.Label, label, StringComparison.Ordinal);

    public ValueTask<CompletionItem> Render(CompletionRenderSettings settings, CompletionRenderContext ctx, CancellationToken cancellationToken)
    {
        var item = new CompletionItem { Label = label, Kind = kind };

        if(settings.IncludeDocumentation)
        {
            // TODO: strip markdown if client only supports text
            item.Documentation = CreateMarkdown(markdownDocumentation);
        }

        // NOTE: VS Code does not currently support lazy resolve of TextEdit, so do not do expensive work here
        // For anything advanced, use additionalTextEdits, which can be resolved lazily.
        if (settings.IncludeTextEdit)
        {
            ComputeCommit(item, settings, ctx);
        }

        if (settings.IncludeAdditionalTextEdits)
        {
            ComputeAdditionalEdits(item, settings, ctx);
        }

        // NOTE: VS Code does not currently support lazy resolve of IncludeCommitCharacters
        if (settings.IncludeCommitCharacters)
        {
            item.CommitCharacters = GetCommitChars(commitKind);
        }

        return new(item);
    }

    static MarkupContent CreateMarkdown(string markdown)
        => new() { Kind = MarkupKind.Markdown, Value = markdown };

    static readonly string[] allCommitChars = { ">", "/", "=", " ", ";", "\"", "'" };
    static readonly string[] attributeCommitChars = { "=", " ", "\"", "'" };
    static readonly string[] tagCommitChars = { ">", "/", " " };
    static readonly string[] entityCommitChars = { ";" };
    static readonly string[] attributeValueCommitChars = { "\"", "'" };

    static string[] GetCommitChars(XmlCommitKind trigger)
        => trigger switch {
            XmlCommitKind.Element => tagCommitChars,
            XmlCommitKind.Attribute => attributeCommitChars,
            XmlCommitKind.AttributeValue => attributeValueCommitChars,
            XmlCommitKind.Entity => entityCommitChars,
            _ => allCommitChars
        };

    void ComputeCommit(CompletionItem item, CompletionRenderSettings settings, CompletionRenderContext ctx)
    {
        var range = ctx.EditRange;

        switch(commitKind)
        {
        case XmlCommitKind.Attribute:
        {
            if(!ctx.CompletionOptions.InsertEmptyAttributeValue || !settings.SupportSnippetFormat)
            {
                return;
            }

            // if there's already an = after this, don't try to add one
            // TODO: we should also avoid adding the ="" if the completion char is '=' or ' '
            // we may be able to workaround this by implementing overtype behavior on the VS Code side
            if(ctx.PreTriggerSourceText.GetNextNonWhitespaceChar(range.End) == '=')
            {
                return;
            }

            // TODO: setting for auto attribute insertion

            // FIXME: get the default attribute quote char from options
            // TODO: the attribute quote char should be detected from the typed quote char: typedChar == '\'' ? '\'' : '"'

            char quoteChar = ctx.CompletionOptions.QuoteChar;

            item.TextEdit = new TextEdit {
                NewText = $"{label}={quoteChar}$0{quoteChar})",
                Range = ctx.EditRange
            };
            item.InsertTextFormat = InsertTextFormat.Snippet;

            return;
        }
        case XmlCommitKind.Element:
        {
            if(!ctx.CompletionOptions.InsertClosingTag) {
                return;
            }

            item.TextEditText = $"{label}>";

            // TODO: committing with / should make element self closing, but only if this does not cause the span to start with a /
            // as that will prevent matching a closing tag item
            return;
        }
        /*
        // TODO: SelfClosingElement should commit as non-self-closing if the commit char is >
        // but LSP does not currently let us alter the TextEdit depending on the commit char
        case XmlCommitKind.SelfClosingElement:
            item.TextEdit = new TextEdit {
                NewText = $"{label}/>",
                Range = ExtendRangeToConsume(range, ctx.PreTriggerSourceText, '>')
            };
            // TODO: if the commit char is /, it should be removed
        }
        */
        }
    }

    void ComputeAdditionalEdits(CompletionItem item, CompletionRenderSettings settings, CompletionRenderContext ctx)
    {
        var range = ctx.EditRange;

        switch(commitKind)
        {
        case XmlCommitKind.Element:
        {
            if(!ctx.CompletionOptions.InsertClosingTag) {
                return;
            }

            // TODO: read-ahead to determine whether we need this closing tag or not

            var closingTag = $"</{label}>";

            // ignore the case where there is a matching closing tag immediately after the completion
            // TODO: ignoreCase should be part of the completion item data
            if(ctx.PreTriggerSourceText.MatchNextNonWhitespace(range.End, closingTag, true))
            {
                return;
            }

            item.AdditionalTextEdits = [
                new TextEdit {
                    NewText = closingTag,
                    Range = new LSP.Range {
                        Start = range.End,
                        End = range.End
                    }
                }
            ];

            return;
        }
        case XmlCommitKind.Comment:
        {
            if(!ctx.CompletionOptions.InsertClosingTag) {
                return;
            }

            // this should probably be handled with brace matching and a separate undo step
            // but this is better than nothing

            // TODO: scan-ahead to determine whether we are already in a comment
            if(ctx.PreTriggerSourceText.MatchNextNonWhitespace(range.End, "-->"))
            {
                return;
            }

            item.AdditionalTextEdits = [
                new TextEdit {
                    NewText = $"-->",
                    Range = new LSP.Range {
                        Start = range.End,
                        End = range.End
                    }
                }
            ];

            return;
        }
        case XmlCommitKind.CData:
        {
            if(!settings.SupportSnippetFormat)
            {
                return;
            }

            // this should probably be handled with brace matching and a separate undo step
            // but this is better than nothing.
            // maybe we can eventually use an on-type formatter to handle this.

            // TODO: scan-ahead to determine whether we are already in a CDATA
            if(ctx.PreTriggerSourceText.MatchNextNonWhitespace(range.End, "]]>"))
            {
                return;
            }

            item.AdditionalTextEdits = [
                new TextEdit {
                    NewText = "]]>",
                    Range = new LSP.Range {
                        Start = range.End,
                        End = range.End
                    }
                }
            ];

            return;
        }
        }
    }

    /*
    static void InsertClosingTags(IAsyncCompletionSession session, ITextBuffer buffer, CompletionItem item)
    {
        // completion may or may not include it depending how it was triggered
        bool includesBracket = item.InsertText[0] == '<';

        var insertTillName = item.InsertText.Substring(includesBracket ? 2 : 1);
        var stack = item.Properties.GetProperty<List<XObject>>(typeof(List<XObject>));
        var elements = new List<XElement>();
        for(int i = stack.Count - 1; i >= 0; i--)
        {
            if(stack[i] is XElement el)
            {
                elements.Add(el);
                if(el.Name.FullName == insertTillName)
                {
                    break;
                }
            }
        }

        ITextSnapshot snapshot = buffer.CurrentSnapshot;
        var span = session.ApplicableToSpan.GetSpan(snapshot);

        // extend the span back to include the <, this logic assumes it's included
        if(!includesBracket)
        {
            span = new SnapshotSpan(span.Start - 1, span.Length + 1);
        }

        var thisLine = snapshot.GetLineFromPosition(span.Start);
        ITextSnapshotLine? prevLine = thisLine.LineNumber > 0 ? snapshot.GetLineFromLineNumber(thisLine.LineNumber - 1) : null;

        // if this completion is the first thing on the current line, reindent the current line
        var thisLineFirstNonWhitespaceOffset = thisLine.GetFirstNonWhitespaceOffset();
        var replaceFirstIndent = thisLineFirstNonWhitespaceOffset.HasValue && thisLineFirstNonWhitespaceOffset + thisLine.Start == span.Start;
        if(replaceFirstIndent)
        {
            span = prevLine is not null
                ? new SnapshotSpan(prevLine.End, span.End)
                : new SnapshotSpan(thisLine.Start, span.End);
        }

        // for consistency, take the newline char from the beginning of this line if possible,
        // else from the end of this line, else from the options
        var newLine = prevLine?.GetLineBreakText() ?? thisLine.GetLineBreakText();
        if(string.IsNullOrEmpty(newLine))
        {
            newLine = session.TextView.Options.GetNewLineCharacter();
        }

        var sb = new StringBuilder();
        foreach(var element in elements)
        {
            var line = snapshot.GetLineFromPosition(element.Span.Start);
            // if the element we're closing was on a different line, and started that line,
            // then put the closing tag on a new line with indentation matching the opening tag
            if(line.LineNumber != thisLine.LineNumber && line.GetFirstNonWhitespaceOffset() is int nonWhitespaceOffset && (nonWhitespaceOffset + line.Start == element.Span.Start))
            {
                var whitespaceSpan = new Span(line.Start, nonWhitespaceOffset);
                var whitespace = snapshot.GetText(whitespaceSpan);
                sb.Append(newLine);
                sb.Append(whitespace);
            }
            sb.Append($"</{element.Name.FullName}>");
        }

        ExtendSpanToConsume(ref span, '>');

        var bufferEdit = buffer.CreateEdit();
        bufferEdit.Replace(span, sb.ToString());
        bufferEdit.Apply();
    }
    */
}
