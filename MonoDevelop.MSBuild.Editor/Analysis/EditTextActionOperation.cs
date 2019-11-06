// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;

using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Editor.Analysis
{
	class EditTextActionOperation : MSBuildActionOperation
	{
		readonly List<Edit> edits = new List<Edit> ();

		public EditTextActionOperation ()
		{
		}

		public sealed override void Apply (IEditorOptions options, ITextBuffer document, CancellationToken cancellationToken, ITextView textView = null)
		{
			bool replicateNewLine = options.GetReplicateNewLineCharacter ();
			var defaultNewLine = options.GetNewLineCharacter ();

			List<(ITrackingPoint point, TextSpan[] spans)> selections = null;
			if (textView != null) {
				foreach (var change in edits) {
					selections ??= new List<(ITrackingPoint point, TextSpan[] spans)> ();
					if (change.RelativeSelections is TextSpan[] selSpans) {
						selections.Add ((
							document.CurrentSnapshot.CreateTrackingPoint (change.Span.Start, PointTrackingMode.Negative),
							selSpans
						));
					}
				}
			}

			using var edit = document.CreateEdit ();
			foreach (var change in edits) {
				string GetText () => GetTextWithCorrectNewLines (replicateNewLine, defaultNewLine, change, edit.Snapshot);
				switch (change.Kind) {
				case Kind.Insert:
					edit.Insert (change.Span.Start, GetText ());
					break;
				case Kind.Replace:
					edit.Replace (change.Span.Start, change.Span.Length, GetText ());
					break;
				case Kind.Delete:
					edit.Delete (change.Span.Start, change.Span.Length);
					break;
				}
			}
			edit.Apply ();

			if (textView != null && selections != null && selections.Count > 0) {
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
		}
		EditTextActionOperation WithEdit (Edit e)
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
			=> WithEdit (new Edit (Kind.Insert, new TextSpan (offset, 0), text, relativeSelections));
		public EditTextActionOperation Replace (int offset, int length, string text, TextSpan[] relativeSelections = null)
			=> WithEdit (new Edit (Kind.Replace, new TextSpan (offset, length), text, relativeSelections));
		public EditTextActionOperation Replace (TextSpan span, string text, TextSpan[] relativeSelections = null)
			=> WithEdit (new Edit (Kind.Replace, span, text, relativeSelections));
		public EditTextActionOperation Delete (int offset, int length)
			=> WithEdit (new Edit (Kind.Delete, new TextSpan (offset, length)));
		public EditTextActionOperation Delete (TextSpan span)
			=> WithEdit (new Edit (Kind.Delete, span));
		public EditTextActionOperation DeleteBetween (int start, int end)
			=> WithEdit (new Edit (Kind.Delete, TextSpan.FromBounds (start, end)));
		public EditTextActionOperation Select (TextSpan span)
			=> WithEdit (new Edit (Kind.Select, new TextSpan (span.Start, 0), relativeSelections: new[] { new TextSpan (0, span.Length) }));

		readonly struct Edit
		{
			public readonly Kind Kind;
			public readonly TextSpan Span;
			public readonly string Text;
			public readonly TextSpan[] RelativeSelections;

			public Edit (Kind kind, TextSpan span, string text = null, TextSpan[] relativeSelections = null)
			{
				Kind = kind;
				Span = span;
				Text = text;
				RelativeSelections = relativeSelections;
			}
		}

		enum Kind
		{
			Insert,
			Replace,
			Delete,
			Select,
		}
	}
}