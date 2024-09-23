// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Microsoft.Build.Shared;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

using MonoDevelop.MSBuild.Editor.CodeActions;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Editor.Analysis
{
	static class MSBuildWorkspaceEditExtensions
	{
		public static IList<MSBuildDocumentEdit> GetDocumentEdits(this MSBuildWorkspaceEdit workspaceEdit, ITextBuffer buffer)
		{
			if (!buffer.Properties.TryGetProperty<ITextDocument> (typeof (ITextDocument), out var doc)) {
				throw new ArgumentException ("The buffer does not have an associated ITextDocument");
			}
			return workspaceEdit.GetDocumentEditsForFile (doc.FilePath);
		}

		public static IList<MSBuildDocumentEdit> GetDocumentEditsForFile (this MSBuildWorkspaceEdit workspaceEdit, string filename)
		{
			var edits = workspaceEdit.Operations.OfType<MSBuildDocumentEdit> ().Where (e => string.Equals (filename, e.Filename, FileUtilities.PathComparison)).ToList ();
			return edits;
		}

		public static void Apply (this MSBuildWorkspaceEdit workspaceEdit, ITextBuffer buffer, CancellationToken cancellationToken, ITextView? textView = null)
		{
			var documentEdits = GetDocumentEdits(workspaceEdit, buffer);

			if (documentEdits.Count != workspaceEdit.Operations.Count) {
				throw new ArgumentException ("Only workspace edits that affect the focused file are currently supported");
			}

			var allTextEdits = documentEdits.SelectMany (e => e.TextEdits).ToList();

			allTextEdits.Apply(buffer, cancellationToken, textView);
		}

		public static void Apply (this IList<MSBuildTextEdit> edits, ITextBuffer document, CancellationToken cancellationToken, ITextView? textView = null)
		{
			var selections = textView != null ? GetSelectionTrackingSpans (edits, document.CurrentSnapshot) : null;

			using var edit = document.CreateEdit ();
			foreach (var change in edits) {
				if (change.NewText.Length == 0) {
					edit.Delete (change.Range.Start, change.Range.Length);
				} else if (change.Range.Length == 1) {
					edit.Insert (change.Range.Start, change.NewText);
				} else {
					edit.Replace (change.Range.Start, change.Range.Length, change.NewText);
				}
			}
			edit.Apply ();

			if (textView is not null && selections != null && selections.Count > 0) {
				ApplySelections (selections, textView);
			}
		}

		static List<(ITrackingPoint point, TextSpan[] spans)>? GetSelectionTrackingSpans (IList<MSBuildTextEdit> edits, ITextSnapshot snapshot)
		{
			List<(ITrackingPoint point, TextSpan[] spans)>? selections = null;
			foreach (var change in edits) {
				selections ??= new List<(ITrackingPoint point, TextSpan[] spans)> ();
				if (change.RelativeSelections is TextSpan[] selSpans) {
					selections.Add ((
						snapshot.CreateTrackingPoint (change.Range.Start, PointTrackingMode.Negative),
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
	}
}