// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

using TextSpan = MonoDevelop.Xml.Dom.TextSpan;

namespace MonoDevelop.MSBuild.Editor.CodeActions;

class MSBuildDocumentEdit (string filename, SourceText? originalText, MSBuildTextEdit[] textEdits) : MSBuildWorkspaceEditOperation (filename)
{
	public MSBuildTextEdit[] TextEdits => textEdits;

	public SourceText? OriginalText { get; } = originalText;
}

class MSBuildTextEdit(TextSpan range, string newText, TextSpan[]? relativeSelections = null)
{
	public string NewText => newText;

	public TextSpan Range => range;

	/// <summary>
	/// If this edit is in the focused document, then these ranges (relative to the beginning of the <see cref="Range"/>) will be selected after the operation is complete.
	/// </summary>
	public TextSpan[]? RelativeSelections => relativeSelections;

	public MSBuildChangeAnnotation? Annotation { get; set; }
}