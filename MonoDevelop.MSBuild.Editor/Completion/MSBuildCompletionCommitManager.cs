// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MonoDevelop.MSBuild.Editor.Completion
{
	class MSBuildCompletionCommitManager : IAsyncCompletionCommitManager
	{
		readonly MSBuildCompletionCommitManagerProvider provider;

		public MSBuildCompletionCommitManager (MSBuildCompletionCommitManagerProvider provider)
		{
			this.provider = provider;
		}

		public IEnumerable<char> PotentialCommitCharacters => new[] { ' ', '(', ')', '.', '-', ';', '[' };

		public bool ShouldCommitCompletion (IAsyncCompletionSession session, SnapshotPoint location, char typedChar, CancellationToken token)
		{
			return PotentialCommitCharacters.Contains (typedChar);
		}

		public CommitResult TryCommit (IAsyncCompletionSession session, ITextBuffer buffer, CompletionItem item, char typedChar, CancellationToken token)
		{
			//var completionKind = item.Properties.PropertyList[item.Properties.PropertyList.Count - 1].Value;
			var completionKind = item.Properties.GetProperty (typeof (MSBuildSpecialCommitKind));
			switch (completionKind) {
			case MSBuildSpecialCommitKind.Element: {
					if (item.InsertText.Equals ("packagereference", StringComparison.OrdinalIgnoreCase)) {
						string insertionText = $"{item.InsertText}/>";
						Insert (session, buffer, insertionText);
						ShiftCaret (session, 2, CaretDirection.Left);
					} else {
						string insertionText = $"{item.InsertText}></{item.InsertText}>";
						Insert (session, buffer, insertionText);
						ShiftCaret (session, item.InsertText.Length + 3, CaretDirection.Left);
					}
					return CommitResult.Handled;
				}
			case MSBuildSpecialCommitKind.Attribute: {
					string insertionText = $"{item.InsertText}=" + @"""""";
					Insert (session, buffer, insertionText);
					ShiftCaret (session, 1, CaretDirection.Left);
					return CommitResult.Handled;
				}
			case MSBuildSpecialCommitKind.PackageReferenceValue: {
					string insertionText = $"{item.InsertText}";
					Insert (session, buffer, insertionText);
					ShiftCaret (session, 1, CaretDirection.Right);
					return CommitResult.Handled;
				}
			}
			if (!item.Properties.TryGetProperty<MSBuildSpecialCommitKind> (typeof (MSBuildSpecialCommitKind), out var kind)) {
				return CommitResult.Unhandled;
			}

			switch (kind) {
			case MSBuildSpecialCommitKind.NewGuid:
				InsertGuid (session, buffer);
				return CommitResult.Handled;

			case MSBuildSpecialCommitKind.ItemReference:
			case MSBuildSpecialCommitKind.PropertyReference:
			case MSBuildSpecialCommitKind.MetadataReference:
				var str = item.InsertText;
				//avoid the double paren
				if (typedChar == '(') {
					str = str.Substring (0, str.Length - 1);
				}
				Insert (session, buffer, str);
				RetriggerCompletion (session.TextView);
				//TODO: insert overtypeable closing paren
				return CommitResult.Handled;
			}

			LoggingService.LogError ($"MSBuild commit manager did not handle unknown special completion kind {kind}");
			return CommitResult.Unhandled;
		}

		private static void ShiftCaret (IAsyncCompletionSession session, int len, CaretDirection caretDirection)
		{
			switch (caretDirection) {
			case CaretDirection.Left:
				for (int i = 0; i < len; i++) {
					session.TextView.Caret.MoveToPreviousCaretPosition ();
				}
				return;
			case CaretDirection.Right:
				for (int i = 0; i < len; i++) {
					session.TextView.Caret.MoveToNextCaretPosition ();
				}
				return;
			case CaretDirection.Top:
				//TO DO
				return;
			case CaretDirection.Down:
				//TO DO
				return;
			}
			return;
		}

		void RetriggerCompletion (ITextView textView)
		{
			Task.Run (async () => {
				await provider.JoinableTaskContext.Factory.SwitchToMainThreadAsync ();
				provider.CommandServiceFactory.GetService (textView).Execute ((v, b) => new InvokeCompletionListCommandArgs (v, b), null);
			});
		}

		void InsertGuid (IAsyncCompletionSession session, ITextBuffer buffer) => Insert (session, buffer, Guid.NewGuid ().ToString ("B").ToUpper ());

		void Insert (IAsyncCompletionSession session, ITextBuffer buffer, string text)
		{
			var span = session.ApplicableToSpan.GetSpan (buffer.CurrentSnapshot);

			var bufferEdit = buffer.CreateEdit ();
			bufferEdit.Replace (span, text);
			bufferEdit.Apply ();
		}

		enum CaretDirection
		{
			Left,
			Right,
			Top,
			Down
		}
	}
}