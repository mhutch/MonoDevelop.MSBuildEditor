// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;

using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Analysis
{
	class EditTextActionOperation : MSBuildActionOperation
	{
		readonly List<Edit> edits = new List<Edit> ();

		public EditTextActionOperation ()
		{
		}

		public sealed override void Apply (IEditorOptions options, ITextBuffer document, CancellationToken cancellationToken)
		{
			bool replicateNewLine = options.GetReplicateNewLineCharacter ();
			var defaultNewLine = options.GetNewLineCharacter ();

			using var edit = document.CreateEdit ();
			foreach (var change in edits) {
				string GetText () => GetTextWithCorrectNewLines (replicateNewLine, defaultNewLine, change, edit.Snapshot);
				switch (change.Kind) {
				case Kind.Insert:
					edit.Insert (change.Offset, GetText ());
					break;
				case Kind.Replace:
					edit.Replace (change.Offset, change.Length, GetText ());
					break;
				case Kind.Delete:
					edit.Delete (change.Offset, change.Length);
					break;
				}
			}
			edit.Apply ();
		}
		EditTextActionOperation WithEdit (Edit e)
		{
			edits.Add (e);
			return this;
		}

		static string GetTextWithCorrectNewLines (bool replicateNewLine, string defaultNewLine, Edit edit, ITextSnapshot snapshot)
		{
			string newLine = null;
			var currentLine = snapshot.GetLineFromPosition (edit.Offset);
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

		public EditTextActionOperation Insert (int offset, string text) => WithEdit (new Edit (Kind.Insert, offset, 0, text));
		public EditTextActionOperation Replace (int offset, int length, string text) => WithEdit (new Edit (Kind.Replace, offset, length, text));
		public EditTextActionOperation Replace (TextSpan span, string text) => WithEdit (new Edit (Kind.Replace, span.Start, span.Length, text));
		public EditTextActionOperation Delete (int offset, int length) => WithEdit (new Edit (Kind.Delete, offset, length, null));
		public EditTextActionOperation Delete (TextSpan span) => WithEdit (new Edit (Kind.Delete, span.Start, span.Length, null));
		public EditTextActionOperation DeleteBetween (int start, int end) => WithEdit (new Edit (Kind.Delete, start, end - start, null));

		readonly struct Edit
		{
			public readonly Kind Kind;
			public readonly int Offset, Length;
			public readonly string Text;

			public Edit (Kind kind, int offset, int length, string text)
			{
				Kind = kind;
				Offset = offset;
				Length = length;
				Text = text;
			}
		}

		enum Kind
		{
			Insert,
			Replace,
			Delete,
		}
	}
}