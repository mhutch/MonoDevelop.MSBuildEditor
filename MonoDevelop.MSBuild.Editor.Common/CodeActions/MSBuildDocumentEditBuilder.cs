// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis.Text;

using MonoDevelop.Xml.Options;

using TextSpan = MonoDevelop.Xml.Dom.TextSpan;

namespace MonoDevelop.MSBuild.Editor.CodeActions;

/// <summary>
/// Helper for building a set of <see cref="MSBuildDocumentEdit"/>s for a file. It automatically takes
/// care of fixing newlines and indentation to match code style and settings.
/// </summary>
/// <param name="filename"></param>
partial class MSBuildDocumentEditBuilder(string filename)
{
	readonly List<Edit> edits = [];

	public MSBuildDocumentEdit GetDocumentEdit(SourceText sourceText, IOptionsReader options, TextFormattingOptionValues textFormat)
	{
		FixNewLinesAndIndentation (edits, sourceText, options, textFormat);

		var textEdits = edits.Select (edit => new MSBuildTextEdit (edit.Span, edit.Text ?? "", edit.RelativeSelections)).ToArray ();

		return new MSBuildDocumentEdit (filename, sourceText, textEdits);
	}

	static void FixNewLinesAndIndentation (List<Edit> edits, SourceText sourceText, IOptionsReader options, TextFormattingOptionValues textFormat)
	{
		var replicateNewLine = options.GetOption(MSBuildEditorOptions.ReplicateNewlineCharacter);
		var defaultNewLine = textFormat.NewLine;
		var indent = GetIndent (textFormat);
		string tabString = new (indent.indentChar, indent.charCount);

		for (int i = 0; i < edits.Count; i++) {
			var edit = edits[i];
			if (edit.Text is not string text || (text.IndexOf ('\n') < 0 && text.IndexOf ('\t') < 0)) {
				continue;
			}

			string emptyNewlineString = GetNewlineForPosition (edit.Span.Start);
			string nonEmptyNewlineString = edit.BaseIndentDepth is int baseIndentDepth && baseIndentDepth > 0
				? emptyNewlineString + new string (indent.indentChar, indent.charCount * baseIndentDepth)
				: emptyNewlineString;

			int GetAdditionalLength (int start, int end)
			{
				int emptyNewlines = 0, nonEmptyNewlines = 0, tabs = 0;
				for (int offset = start; offset < end; offset++) {
					char c = text[offset];
					if (c == '\n') {
						if (offset + 1 < text.Length && text[offset + 1] == '\n') {
							emptyNewlines++;
						} else {
							nonEmptyNewlines++;
						}
					} else if (c == '\t') {
						tabs++;
					}
				}
				return emptyNewlines * Math.Max (emptyNewlineString.Length - 1, 0)
					 + nonEmptyNewlines * Math.Max (nonEmptyNewlineString.Length - 1, 0)
					 + tabs * Math.Max (tabString.Length - 1, 0);
			}

			if (edit.RelativeSelections != null) {
				for (int s = 0; s < edit.RelativeSelections.Length; s++) {
					ref var sel = ref edit.RelativeSelections[s];
					var newStart = sel.Start + GetAdditionalLength (0, sel.Start);
					var newLength = sel.Length + GetAdditionalLength (sel.Start, sel.Start + sel.Length);
					sel = new TextSpan (newStart, newLength);
				}
			}

			var sb = new StringBuilder (text.Length + GetAdditionalLength (0, text.Length));

			for (int offset = 0; offset < text.Length; offset++) {
				char c = text[offset];
				if (c == '\n') {
					if (offset + 1 < text.Length && text[offset + 1] == '\n') {
						sb.Append (emptyNewlineString);
					} else {
						sb.Append (nonEmptyNewlineString);
					}
				} else if (c == '\t') {
					sb.Append (tabString);
				} else {
					sb.Append (c);
				}
			}

			edit.Text = sb.ToString ();
			edits[i] = edit;
		}

		string GetNewlineForPosition (int position)
		{
			string? newLineText = null;
			if (replicateNewLine) {
				newLineText = sourceText.GetLineBreakTextForLineContainingOffset (position);
			}
			return newLineText ?? defaultNewLine ?? "\n";
		}

		static (char indentChar, int charCount) GetIndent (TextFormattingOptionValues options)
		{
			var indentSize = options.IndentSize;
			if (options.ConvertTabsToSpaces) {
				return (' ', indentSize);
			} else {
				var tabSize = options.TabSize;
				var tabsPerIndent = tabSize > 0 ? indentSize / tabSize : 1;
				return ('\t', tabsPerIndent);
			}
		}
	}

	MSBuildDocumentEditBuilder WithEdit (Edit e)
	{
		edits.Add (e);
		return this;
	}

	public MSBuildDocumentEditBuilder Insert (int offset, string text, TextSpan[]? relativeSelections = null, int baseIndentDepth = 0)
		=> WithEdit (new Edit (new TextSpan (offset, 0), text, relativeSelections, baseIndentDepth));
	public MSBuildDocumentEditBuilder InsertAndSelect (int offset, string text, TextSpan[]? relativeSelections = null, int baseIndentDepth = 0)
		=> WithEdit (new Edit (new TextSpan (offset, 0), text, relativeSelections ?? new[] { new TextSpan (0, text.Length) }, baseIndentDepth));
	public MSBuildDocumentEditBuilder InsertAndSelect (int offset, string textWithMarkers, char selectionMarker, int baseIndentDepth = 0)
		=> WithEdit (Edit.WithMarkedSelection (new TextSpan (offset, 0), textWithMarkers, selectionMarker, baseIndentDepth));

	public MSBuildDocumentEditBuilder Replace (int offset, int length, string text, int baseIndentDepth = 0)
		=> WithEdit (new Edit (new TextSpan (offset, length), text));
	public MSBuildDocumentEditBuilder Replace (TextSpan span, string text, int baseIndentDepth = 0)
		=> WithEdit (new Edit (span, text));

	public MSBuildDocumentEditBuilder ReplaceAndSelect (int offset, int length, string text, TextSpan[]? relativeSelections = null, int baseIndentDepth = 0)
		=> WithEdit (new Edit (new TextSpan (offset, length), text, relativeSelections ?? new[] { new TextSpan (0, text.Length) }, baseIndentDepth));
	public MSBuildDocumentEditBuilder ReplaceAndSelect (TextSpan span, string text, TextSpan[]? relativeSelections = null, int baseIndentDepth = 0)
		=> WithEdit (new Edit (span, text, relativeSelections ?? new[] { new TextSpan (0, text.Length) }, baseIndentDepth));

	public MSBuildDocumentEditBuilder ReplaceAndSelect (int offset, int length, string textWithMarkers, char selectionMarker, int baseIndentDepth = 0)
		=> WithEdit (Edit.WithMarkedSelection (new TextSpan (offset, length), textWithMarkers, selectionMarker, baseIndentDepth));
	public MSBuildDocumentEditBuilder ReplaceAndSelect (TextSpan span, string textWithMarkers, char selectionMarker, int baseIndentDepth = 0)
		=> WithEdit (Edit.WithMarkedSelection (span, textWithMarkers, selectionMarker, baseIndentDepth));

	public MSBuildDocumentEditBuilder Delete (int offset, int length)
		=> WithEdit (new Edit (new TextSpan (offset, length)));
	public MSBuildDocumentEditBuilder Delete (TextSpan span)
		=> WithEdit (new Edit (span));

	public MSBuildDocumentEditBuilder DeleteBetween (int start, int end)
		=> WithEdit (new Edit (TextSpan.FromBounds (start, end)));

	public MSBuildDocumentEditBuilder Select (TextSpan span)
		=> WithEdit (new Edit (new TextSpan (span.Start, 0), relativeSelections: new[] { new TextSpan (0, span.Length) }));
}
