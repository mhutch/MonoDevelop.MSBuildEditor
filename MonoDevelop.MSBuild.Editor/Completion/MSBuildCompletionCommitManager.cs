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

			if (typedChar == '.') {
				if (session.Properties.TryGetProperty (typeof (MSBuildCompletionSource.NuGetSearchUpdater), out MSBuildCompletionSource.NuGetSearchUpdater searchInfo)) {
					return false;
				}
			}

			//TODO: further refine this based on the trigger
			return true;
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

			LogUnhandledItemKind (logger, kind);
			return CommitResult.Unhandled;
		}

		void RetriggerCompletion (ITextView textView)
		{
			Task.Run (async () => {
				await provider.JoinableTaskContext.Factory.SwitchToMainThreadAsync ();
				provider.CommandServiceFactory.GetService (textView).Execute ((v, b) => new InvokeCompletionListCommandArgs (v, b), null);
			}).CatchAndLogWarning (logger);
		}

		void InsertGuid (IAsyncCompletionSession session, ITextBuffer buffer) => Insert (session, buffer, Guid.NewGuid ().ToString ("B").ToUpper ());

		void Insert (IAsyncCompletionSession session, ITextBuffer buffer, string text)
		{
			var span = session.ApplicableToSpan.GetSpan (buffer.CurrentSnapshot);

			var bufferEdit = buffer.CreateEdit ();
			bufferEdit.Replace (span, text);
			bufferEdit.Apply ();
		}

		[LoggerMessage (EventId = 0, Level = LogLevel.Error, Message = "MSBuild commit manager did not handle unknown special completion kind {kind}")]
		static partial void LogUnhandledItemKind (ILogger logger, MSBuildSpecialCommitKind kind);
	}
}