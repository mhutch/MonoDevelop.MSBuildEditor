// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;

namespace MonoDevelop.MSBuild.Editor.Completion
{
	class MSBuildCompletionCommitManager : IAsyncCompletionCommitManager
	{
		readonly MSBuildCompletionCommitManagerProvider provider;

		public MSBuildCompletionCommitManager (MSBuildCompletionCommitManagerProvider provider)
		{
			this.provider = provider;
		}

		public IEnumerable<char> PotentialCommitCharacters => Array.Empty<char> ();

		public bool ShouldCommitCompletion (IAsyncCompletionSession session, SnapshotPoint location, char typedChar, CancellationToken token)
		{
			return false;
		}

		public CommitResult TryCommit (IAsyncCompletionSession session, ITextBuffer buffer, CompletionItem item, char typedChar, CancellationToken token)
		{
			if (!item.Properties.TryGetProperty<MSBuildSpecialCommitKind> (typeof (MSBuildSpecialCommitKind), out var kind)) {
				return CommitResult.Unhandled;
			}

			switch (kind) {
			case MSBuildSpecialCommitKind.NewGuid:
				InsertGuid (session, buffer);
				return CommitResult.Handled;
			case MSBuildSpecialCommitKind.ItemReference:
			case MSBuildSpecialCommitKind.PropertyReference:
				Insert (session, buffer, item.InsertText);
				RetriggerCompletion (session.TextView);
				//TODO: insert trailing )
				return CommitResult.Handled;
			}

			LoggingService.LogError ($"MSBuild commit manager did not handle unknown special completion kind {kind}");
			return CommitResult.Unhandled;
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
	}
}