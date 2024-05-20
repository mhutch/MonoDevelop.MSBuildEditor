// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;

using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Editor.Analysis
{
	class EditTextActionOperation : MSBuildCodeActionOperation
	{
		readonly List<Edit> edits = new List<Edit> ();

		public EditTextActionOperation ()
		{
		}

		public sealed override void Apply (IEditorOptions options, ITextBuffer document, CancellationToken cancellationToken, ITextView? textView = null)
		{
			FixNewLinesAndIndentation (edits, options, document.CurrentSnapshot);

			var selections = textView != null ? GetSelectionTrackingSpans (edits, document.CurrentSnapshot) : null;

			using var edit = document.CreateEdit ();
			foreach (var change in edits) {
				switch (change.Kind) {
				case EditKind.Insert:
					edit.Insert (change.Span.Start, change.Text);
					break;
				case EditKind.Replace:
					edit.Replace (change.Span.Start, change.Span.Length, change.Text);
					break;
				case EditKind.Delete:
					edit.Delete (change.Span.Start, change.Span.Length);
					break;
				}
			}
			edit.Apply ();

			if (textView is not null && selections != null && selections.Count > 0) {
				ApplySelections (selections, textView);
			}
		}

		static List<(ITrackingPoint point, TextSpan[] spans)>? GetSelectionTrackingSpans (List<Edit> edits, ITextSnapshot snapshot)
		{
			List<(ITrackingPoint point, TextSpan[] spans)>? selections = null;
			foreach (var change in edits) {
				selections ??= new List<(ITrackingPoint point, TextSpan[] spans)> ();
				if (change.RelativeSelections is TextSpan[] selSpans) {
					selections.Add ((
						snapshot.CreateTrackingPoint (change.Span.Start, PointTrackingMode.Negative),
						selSpans
					));
				}
			}
			return selections;
		}

		static void ApplySelections (List<(ITrackingPoint point, TextSpan[] spans)> selections, ITextView textView)
		{
			var broker = textView.GetMultiSelectionBroker ();
			var snapshot = textView.TextSnapshot;
			bool isFirst = true;
			foreach (var (point, spans) in selections) {
				var p = point.GetPoint (snapshot);
				foreach (var span in spans) {
					var s = new Selection (new SnapshotSpan (p + span.Start, span.Length));
					broker.AddSelection (s);
					if (isFirst) {
						broker.TrySetAsPrimarySelection (s);
						broker.ClearSecondarySelections ();
						isFirst = false;
					}
				}
			}
		}

		static void FixNewLinesAndIndentation (List<Edit> edits, IEditorOptions options, ITextSnapshot snapshot)
		{
			var replicateNewLine = options.GetReplicateNewLineCharacter ();
			var defaultNewLine = options.GetNewLineCharacter () ?? "\n";
			var indent = GetIndent (options);
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
					var currentLine = snapshot.GetLineFromPosition (position);
					newLineText = currentLine?.GetLineBreakText ();
				}
				return newLineText ?? defaultNewLine ?? "\n";
			}

			static (char indentChar, int charCount) GetIndent (IEditorOptions options)
			{
				var indentSize = options.GetIndentSize ();
				if (options.IsConvertTabsToSpacesEnabled ()) {
					return (' ', indentSize);
				} else {
					var tabSize = options.GetTabSize ();
					var tabsPerIndent = tabSize > 0? indentSize / tabSize : 1;
					return ('\t', tabsPerIndent);
				}
			}
		}

		EditTextActionOperation WithEdit (Edit e)
		{
			edits.Add (e);
			return this;
		}

		public EditTextActionOperation Insert (int offset, string text, TextSpan[]? relativeSelections = null, int baseIndentDepth = 0)
			=> WithEdit (new Edit (EditKind.Insert, new TextSpan (offset, 0), text, relativeSelections, baseIndentDepth));
		public EditTextActionOperation InsertAndSelect (int offset, string text, TextSpan[]? relativeSelections = null, int baseIndentDepth = 0)
			=> WithEdit (new Edit (EditKind.Insert, new TextSpan (offset, 0), text, relativeSelections ?? new[] { new TextSpan (0, text.Length) }, baseIndentDepth));
		public EditTextActionOperation InsertAndSelect (int offset, string textWithMarkers, char selectionMarker, int baseIndentDepth = 0)
			=> WithEdit (Edit.WithMarkedSelection (EditKind.Insert, new TextSpan (offset, 0), textWithMarkers, selectionMarker, baseIndentDepth));

		public EditTextActionOperation Replace (int offset, int length, string text, int baseIndentDepth = 0)
			=> WithEdit (new Edit (EditKind.Replace, new TextSpan (offset, length), text));
		public EditTextActionOperation Replace (TextSpan span, string text, int baseIndentDepth = 0)
			=> WithEdit (new Edit (EditKind.Replace, span, text));

		public EditTextActionOperation ReplaceAndSelect (int offset, int length, string text, TextSpan[]? relativeSelections = null, int baseIndentDepth = 0)
			=> WithEdit (new Edit (EditKind.Replace, new TextSpan (offset, length), text, relativeSelections ?? new[] { new TextSpan (0, text.Length) }, baseIndentDepth));
		public EditTextActionOperation ReplaceAndSelect (TextSpan span, string text, TextSpan[]? relativeSelections = null, int baseIndentDepth = 0)
			=> WithEdit (new Edit (EditKind.Replace, span, text, relativeSelections ?? new[] { new TextSpan (0, text.Length) }, baseIndentDepth));

		public EditTextActionOperation ReplaceAndSelect (int offset, int length, string textWithMarkers, char selectionMarker, int baseIndentDepth = 0)
			=> WithEdit (Edit.WithMarkedSelection (EditKind.Replace, new TextSpan (offset, length), textWithMarkers, selectionMarker, baseIndentDepth));
		public EditTextActionOperation ReplaceAndSelect (TextSpan span, string textWithMarkers, char selectionMarker, int baseIndentDepth = 0)
			=> WithEdit (Edit.WithMarkedSelection (EditKind.Replace, span, textWithMarkers, selectionMarker, baseIndentDepth));

		public EditTextActionOperation Delete (int offset, int length)
			=> WithEdit (new Edit (EditKind.Delete, new TextSpan (offset, length)));
		public EditTextActionOperation Delete (TextSpan span)
			=> WithEdit (new Edit (EditKind.Delete, span));

		public EditTextActionOperation DeleteBetween (int start, int end)
			=> WithEdit (new Edit (EditKind.Delete, TextSpan.FromBounds (start, end)));

		public EditTextActionOperation Select (TextSpan span)
			=> WithEdit (new Edit (EditKind.Select, new TextSpan (span.Start, 0), relativeSelections: new[] { new TextSpan (0, span.Length) }));

		[DebuggerDisplay ("{Kind}@{Span}:'{Text}'")]
		struct Edit
		{
			public EditKind Kind { get; }
			public TextSpan Span { get; }
			public string? Text { get; internal set; }
			public TextSpan[]? RelativeSelections { get; }
			public int? BaseIndentDepth { get; }

			public Edit (EditKind kind, TextSpan span, string? text = null, TextSpan[]? relativeSelections = null, int? baseIndentDepth = null)
			{
				Kind = kind;
				Span = span;
				Text = text;
				RelativeSelections = relativeSelections;
				BaseIndentDepth = baseIndentDepth;
			}

			public static Edit WithMarkedSelection (EditKind kind, TextSpan span, string textWithMarkers, char selectionMarker, int? baseIndentDepth = null)
			{
				(var text, var relativeSelections) = ExtractSpans (textWithMarkers, selectionMarker);

				return new Edit (kind, span, text, relativeSelections.ToArray (), baseIndentDepth);
			}

			static (string text, List<TextSpan> selections) ExtractSpans (string textWithMarkers, char selectionMarker)
			{
				var spans = new List<TextSpan> ();

				int spanStart = -1;
				var cleanTextBuilder = new StringBuilder (textWithMarkers.Length);

				for (int i = 0; i < textWithMarkers.Length; i++) {
					char c = textWithMarkers[i];
					if (c == selectionMarker) {
						if (spanStart < 0) {
							spanStart = cleanTextBuilder.Length;
						} else {
							spans.Add (new TextSpan (spanStart, cleanTextBuilder.Length));
							spanStart = -1;
						}
					} else {
						cleanTextBuilder.Append (c);
					}
				}

				if (spanStart > -1) {
					throw new ArgumentException ("Odd number of markers");
				}

				return new (cleanTextBuilder.ToString (), spans);
			}
		}

		enum EditKind
		{
			Insert,
			Replace,
			Delete,
			Select,
		}
	}
}