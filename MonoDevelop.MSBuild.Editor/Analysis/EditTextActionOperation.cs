// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Diagnostics;
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

		public sealed override void Apply (IEditorOptions options, ITextBuffer document, CancellationToken cancellationToken, ITextView textView = null)
		{
			FixNewlines (edits, options, document.CurrentSnapshot);

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

			if (selections != null && selections.Count > 0) {
				ApplySelections (selections, textView);
			}
		}

		static List<(ITrackingPoint point, TextSpan[] spans)> GetSelectionTrackingSpans (List<Edit> edits, ITextSnapshot snapshot)
		{
			List<(ITrackingPoint point, TextSpan[] spans)> selections = null;
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

		static void FixNewlines (List<Edit> edits, IEditorOptions options, ITextSnapshot snapshot)
		{
			var replicateNewLine = options.GetReplicateNewLineCharacter ();
			var defaultNewLine = options.GetNewLineCharacter ();

			for (int i = 0; i < edits.Count; i++) {
				var edit = edits[i];
				if (edit.Text == null || edit.Text.IndexOf ('\n') < 0) {
					continue;
				}

				string newLine = null;
				var currentLine = snapshot.GetLineFromPosition (edit.Span.Start);
				if (replicateNewLine) {
					newLine = currentLine.GetLineBreakText ();
				}
				if (string.IsNullOrEmpty (newLine)) {
					newLine = defaultNewLine;
				}
				if (newLine == "\n") {
					continue;
				}

				int CountNewlines (int start, int length)
				{
					int count = 0;
					for (int offset = start; offset < start + length; offset++) {
						if (edit.Text[offset] == '\n') {
							count++;
						}
					}
					return count;
				}

				if (edit.RelativeSelections != null) {
					for (int s = 0; s < edit.RelativeSelections.Length; s++) {
						ref var sel = ref edit.RelativeSelections[s];
						var newStart = sel.Start + CountNewlines (0, sel.Start);
						var newLength = sel.Length + CountNewlines (sel.Start, sel.Length);
						sel = new TextSpan (newStart, newLength);
					}
				}

				edit.Text = edit.Text.Replace ("\n", newLine);

				edits[i] = edit;
			}
		}

		internal EditTextActionOperation WithEdit (Edit e)
		{
			edits.Add (e);
			return this;
		}

		static string GetTextWithCorrectNewLines (bool replicateNewLine, string defaultNewLine, Edit edit, ITextSnapshot snapshot)
		{
			string newLine = null;
			var currentLine = snapshot.GetLineFromPosition (edit.Span.Start);
			if (replicateNewLine) {
				newLine = currentLine.GetLineBreakText ();
			}
			if (string.IsNullOrEmpty (newLine)) {
				newLine = defaultNewLine;
			}
			if (newLine != "\n") {
				return edit.Text.Replace ("\n", newLine);
			}
			return edit.Text;
		}

		public EditTextActionOperation Insert (int offset, string text, TextSpan[] relativeSelections = null)
			=> WithEdit (new Edit (EditKind.Insert, new TextSpan (offset, 0), text, relativeSelections));
		public EditTextActionOperation Replace (int offset, int length, string text, TextSpan[] relativeSelections = null)
			=> WithEdit (new Edit (EditKind.Replace, new TextSpan (offset, length), text, relativeSelections));
		public EditTextActionOperation Replace (TextSpan span, string text, TextSpan[] relativeSelections = null)
			=> WithEdit (new Edit (EditKind.Replace, span, text, relativeSelections));
		public EditTextActionOperation Delete (int offset, int length)
			=> WithEdit (new Edit (EditKind.Delete, new TextSpan (offset, length)));
		public EditTextActionOperation Delete (TextSpan span)
			=> WithEdit (new Edit (EditKind.Delete, span));
		public EditTextActionOperation DeleteBetween (int start, int end)
			=> WithEdit (new Edit (EditKind.Delete, TextSpan.FromBounds (start, end)));
		public EditTextActionOperation Select (TextSpan span)
			=> WithEdit (new Edit (EditKind.Select, new TextSpan (span.Start, 0), relativeSelections: new[] { new TextSpan (0, span.Length) }));

		[DebuggerDisplay("{Kind}@{Span}:'{Text}'")]
		internal struct Edit
		{
			public EditKind Kind { get; }
			public TextSpan Span { get; }
			public string Text { get; internal set; }
			public TextSpan[] RelativeSelections { get; }

			public Edit (EditKind kind, TextSpan span, string text = null, TextSpan[] relativeSelections = null)
			{
				Kind = kind;
				Span = span;
				Text = text;
				RelativeSelections = relativeSelections;
			}
		}

		internal enum EditKind
		{
			Insert,
			Replace,
			Delete,
			Select,
		}
	}
}