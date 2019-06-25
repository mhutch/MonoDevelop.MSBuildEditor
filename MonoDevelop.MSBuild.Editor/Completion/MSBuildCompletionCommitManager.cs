// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;

using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;

namespace MonoDevelop.MSBuild.Editor.Completion
{
	class MSBuildCompletionCommitManager : IAsyncCompletionCommitManager
	{
		public IEnumerable<char> PotentialCommitCharacters => Array.Empty<char> ();

		public bool ShouldCommitCompletion (IAsyncCompletionSession session, SnapshotPoint location, char typedChar, CancellationToken token)
		{
			return false;
		}

		public CommitResult TryCommit (IAsyncCompletionSession session, ITextBuffer buffer, CompletionItem item, char typedChar, CancellationToken token)
		{
			if (!item.Properties.TryGetProperty<MSBuildSpecialCompletionKind> (typeof (MSBuildSpecialCompletionKind), out var kind)) {
				return CommitResult.Unhandled;
			}

			switch (kind) {
			case MSBuildSpecialCompletionKind.NewGuid:
				InsertGuid (session, buffer);
				return CommitResult.Handled;
			}

			LoggingService.LogError ($"MSBuild commit manager did not handle unknown special completion kind {kind}");
			return CommitResult.Unhandled;
		}

		void InsertGuid (IAsyncCompletionSession session, ITextBuffer buffer)
		{
			var span = session.ApplicableToSpan.GetSpan (buffer.CurrentSnapshot);

			var bufferEdit = buffer.CreateEdit ();
			bufferEdit.Replace (span, Guid.NewGuid ().ToString ("B").ToUpper ());
			bufferEdit.Apply ();
		}
	}
}