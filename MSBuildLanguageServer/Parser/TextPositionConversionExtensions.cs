// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.LanguageServer;
using Microsoft.CodeAnalysis.Text;

using RoslynTextSpan = Microsoft.CodeAnalysis.Text.TextSpan;
using XTextSpan = MonoDevelop.Xml.Dom.TextSpan;

using LSP = Roslyn.LanguageServer.Protocol;
using MonoDevelop.MSBuild.Language;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Parser;

static class TextPositionConversionExtensions
{
    public static int ToOffset(this LinePosition position, SourceText text) => text.Lines[position.Line].Start + position.Character;

    public static XTextSpan ToXTextSpan(this LinePositionSpan span, SourceText text)
        => XTextSpan.FromBounds(span.Start.ToOffset (text), span.End.ToOffset (text));

    public static XTextSpan ToRoslynTextSpan(this LinePositionSpan span, SourceText text)
        => XTextSpan.FromBounds(span.Start.ToOffset(text), span.End.ToOffset(text));

    public static LinePositionSpan GetLinePositionSpan(this SourceText sourceText, int start, int length)
        => sourceText.Lines.GetLinePositionSpan(new RoslynTextSpan(start, length));

    public static LinePositionSpan ToLinePositionSpan(this XTextSpan span, SourceText sourceText)
        => sourceText.Lines.GetLinePositionSpan(new RoslynTextSpan(span.Start, span.Length));

    public static LSP.Position GetLspPosition(this SourceText sourceText, int offset)
        => ProtocolConversions.LinePositionToPosition(sourceText.Lines.GetLinePosition(offset));

    public static LSP.Range GetLspRange(this SourceText sourceText, int start, int length)
        => ProtocolConversions.TextSpanToRange(new RoslynTextSpan(start, length), sourceText);

    public static LSP.Range ToLspRange(this XTextSpan span, SourceText sourceText)
        => ProtocolConversions.TextSpanToRange(span.ToRoslynTextSpan (), sourceText);

    public static LSP.Range ToLspRange(this RoslynTextSpan span, SourceText sourceText)
        => ProtocolConversions.TextSpanToRange(span, sourceText);
    public static LSP.Range ToLspRange(this MSBuildResolveResult rr, SourceText sourceText)
        => new RoslynTextSpan(rr.ReferenceOffset, rr.ReferenceLength).ToLspRange (sourceText);

    public static RoslynTextSpan ToRoslynTextSpan(this XTextSpan span) => new RoslynTextSpan(span.Start, span.Length);
    public static XTextSpan ToXTextSpan(this XTextSpan span) => new XTextSpan(span.Start, span.Length);
}