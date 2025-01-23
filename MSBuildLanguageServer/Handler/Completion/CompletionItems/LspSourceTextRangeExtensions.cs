// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Roslyn.LanguageServer.Protocol;
using LSP = Roslyn.LanguageServer.Protocol;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.LanguageServer;
using MonoDevelop.MSBuild.Editor.LanguageServer.Parser;
using MonoDevelop.Xml.Parser;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler.Completion.CompletionItems;

static class LspSourceTextRangeExtensions
{
    public static LSP.Range ExtendRangeToConsume(this SourceText sourceText, LSP.Range range, char charToConsume)
    {
        int offset = sourceText.Lines.GetPosition(ProtocolConversions.PositionToLinePosition(range.End));
        if(sourceText.Length > offset && sourceText[offset] == charToConsume)
        {
            offset++;
            return new LSP.Range {
                Start = range.Start,
                End = sourceText.GetLspPosition(offset)
            };
        }
        return range;
    }

    public static char GetNextNonWhitespaceChar(this SourceText sourceText, Position position)
    {
        int offset = sourceText.Lines.GetPosition(ProtocolConversions.PositionToLinePosition(position));
        int max = Math.Min(offset + 5000, sourceText.Length);
        while(offset < max)
        {
            char c = sourceText[offset++];
            if (!XmlChar.IsWhitespace(c))
            {
                return c;
            }
        }
        return '\0';
    }

    public static bool MatchNextNonWhitespace(this SourceText sourceText, LSP.Position position, string match, bool ignoreCase = false)
    {
        int offset = sourceText.Lines.GetPosition(ProtocolConversions.PositionToLinePosition(position));
        int max = Math.Min(offset + 5000, sourceText.Length - match.Length - 1);

        while(XmlChar.IsWhitespace(sourceText[offset++]))
        {
            if (offset >= max)
            {
                return false;
            }
        }

        if (offset + match.Length >= sourceText.Length)
        {
            return false;
        }

        var possibleMatch = sourceText.GetText(offset, match.Length);
        return string.Equals(possibleMatch, match, ignoreCase? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }
}