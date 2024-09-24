// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace MonoDevelop.MSBuild.Editor.CodeActions;

static class SourceTextExtensions
{
	public static string? GetLineBreakTextForLineContainingOffset (this SourceText text, int offset)
	{
		var currentLine = text.Lines.GetLineFromPosition (offset);
		return text.GetLineBreakTextForLine (currentLine);
	}

	public static string? GetLineBreakTextForLine (this SourceText text, TextLine line)
	{
		int length = line.EndIncludingLineBreak - line.End;
		if (length == 0) {
			return null;
		}
		if (length == 1) {
			return text[line.End].ToString ();
		}
		return $"{text[line.End]}{text[line.End + 1]}";
	}
}