// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;

using MonoDevelop.Xml.Logging;

using static MonoDevelop.MSBuild.Language.ExpressionCompletion;

namespace MonoDevelop.MSBuild.Editor.Completion
{
	partial class MSBuildCompletionCommitManager : IAsyncCompletionCommitManager
	{
		readonly MSBuildCompletionCommitManagerProvider provider;
		readonly ILogger<MSBuildCompletionSource> logger;

		public MSBuildCompletionCommitManager (MSBuildCompletionCommitManagerProvider provider, ILogger<MSBuildCompletionSource> logger)
		{
			this.provider = provider;
			this.logger = logger;
		}

		static readonly char[] commitChars = new[] { ' ', '(', ')', '.', '-', ';', '[' };

		public IEnumerable<char> PotentialCommitCharacters => commitChars;

		public bool ShouldCommitCompletion (IAsyncCompletionSession session, SnapshotPoint location, char typedChar, CancellationToken token)
		{
			try {
				return ShouldCommitCompletionInternal (session, location, typedChar, token);
			} catch (Exception ex) {
				logger.LogInternalException (ex);
				throw;
			}
		}

		bool ShouldCommitCompletionInternal (IAsyncCompletionSession session, SnapshotPoint location, char typedChar, CancellationToken token)
		{
			if (!PotentialCommitCharacters.Contains (typedChar)) {
				return false;
			}

			// only handle sessions that MSBuild completion triggering participated in.
			// the XML commit manager will handle sessions triggered by the base XML completion source.
			// although we aren't told what exact item we might be committing yet, the trigger tells us enough
			// about the kind of item to allow us to specialize the commit chars
			if (!session.Properties.TryGetProperty (typeof (TriggerState), out TriggerState triggerState)) {
				return false;
			}

			// NOTE: Returning false will not actually prevent the item from getting committed as another commit manager might handle it.
			// Instead, we must cancel the commit in TryCommit.
			/*
			if (typedChar == '.') {
				if (session.Properties.TryGetProperty (typeof (MSBuildCompletionSource.NuGetSearchUpdater), out MSBuildCompletionSource.NuGetSearchUpdater searchInfo)) {
					return false;
				}
			}
			*/

			//TODO: further refine this based on the trigger
			return true;
		}

		public CommitResult TryCommit (IAsyncCompletionSession session, ITextBuffer buffer, CompletionItem item, char typedChar, CancellationToken token)
		{
			try {
				return TryCommitInternal (session, buffer, item, typedChar, token);
			} catch (Exception ex) {
				logger.LogInternalException (ex);
				throw;
			}
		}

		static readonly CommitResult CommitCancel = new (true, CommitBehavior.CancelCommit);

		CommitResult TryCommitInternal (IAsyncCompletionSession session, ITextBuffer buffer, CompletionItem item, char typedChar, CancellationToken token)
		{
			if (session.Properties.TryGetProperty (typeof (MSBuildCommitSessionKind), out MSBuildCommitSessionKind sessionKind)) {
				switch (sessionKind) {
				case MSBuildCommitSessionKind.Values:
					if (typedChar == '.') {
						return CommitCancel;
					}
					break;
				default:
					throw new InvalidOperationException ($"Unhandled MSBuildCommitSessionKind value {sessionKind}");
				}
			}

			if (item.Properties.TryGetProperty (typeof (MSBuildCommitItemKind), out MSBuildCommitItemKind itemKind)) {
				switch (itemKind) {
				case MSBuildCommitItemKind.NewGuid:
					InsertGuid (session, buffer);
					return CommitResult.Handled;

				case MSBuildCommitItemKind.ItemReference:
				case MSBuildCommitItemKind.PropertyReference:
				case MSBuildCommitItemKind.MetadataReference:
					var str = item.InsertText;
					//avoid the double paren
					if (typedChar == '(') {
						str = str.Substring (0, str.Length - 1);
					}
					Insert (session, buffer, str);
					RetriggerCompletion (session.TextView);
					//TODO: insert overtypeable closing paren
					return CommitResult.Handled;
				default:
					throw new InvalidOperationException ($"Unhandled MSBuildCommitItemKind value {itemKind}");
				}
			}

			return CommitResult.Unhandled;
		}

		void RetriggerCompletion (ITextView textView)
		{
			Task.Run (async () => {
				await provider.JoinableTaskContext.Factory.SwitchToMainThreadAsync ();
				provider.CommandServiceFactory.GetService (textView).Execute ((v, b) => new InvokeCompletionListCommandArgs (v, b), null);
			}).LogTaskExceptionsAndForget (logger);
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