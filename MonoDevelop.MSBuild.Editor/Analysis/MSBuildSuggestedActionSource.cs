// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

using MonoDevelop.MSBuild.Editor.Completion;
using MonoDevelop.Xml.Editor.Completion;

namespace MonoDevelop.MSBuild.Editor.Analysis
{
	sealed class MSBuildSuggestedActionSource : ISuggestedActionsSource
	{
		readonly MSBuildSuggestedActionsSourceProvider provider;
		readonly ITextView textView;
		readonly ITextBuffer textBuffer;
		MSBuildBackgroundParser parser;

		public MSBuildSuggestedActionSource (MSBuildSuggestedActionsSourceProvider provider, ITextView textView, ITextBuffer textBuffer)
		{
			this.provider = provider;
			this.textView = textView;
			this.textBuffer = textBuffer;

			parser = provider.ParserProvider.GetParser (textBuffer);
			parser.ParseCompleted += ParseCompleted;
		}

		void ParseCompleted (object sender, ParseCompletedEventArgs<MSBuildParseResult> e)
		{
			SuggestedActionsChanged?.Invoke (this, EventArgs.Empty);
		}

		public event EventHandler<EventArgs> SuggestedActionsChanged;

		public IEnumerable<SuggestedActionSet> GetSuggestedActions (ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken)
		{
			var fixes = GetSuggestedActionsAsync (requestedActionCategories, range, cancellationToken).WaitAndGetResult (cancellationToken);
			if (fixes == null) {
				yield break;
			}

			foreach (var fix in fixes) {
				yield return new SuggestedActionSet (
					null,
					new ISuggestedAction[] {
						provider.SuggestedActionFactory.CreateSuggestedAction (provider.PreviewService, textView.Options, textBuffer, fix)
					});
			}
		}

		async Task<List<MSBuildCodeFix>> GetSuggestedActionsAsync (ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken)
		{
			// do this first as we are on the UI thread at this point, and can avoid switching to it later
			var possibleSelection = TryGetSelectedSpan ();

			var result = await parser.GetOrProcessAsync (range.Snapshot, cancellationToken);

			List<MSBuildCodeFix> actions = null;

			if (requestedActionCategories.Contains (PredefinedSuggestedActionCategoryNames.CodeFix)) {
				actions = await provider.CodeFixService.GetFixes (textBuffer, result, range, cancellationToken);
			}

			if (possibleSelection is SnapshotSpan selection && requestedActionCategories.Contains (PredefinedSuggestedActionCategoryNames.Refactoring)) {
				var refactorings = await provider.RefactoringService.GetRefactorings (result, selection, cancellationToken);
				if (actions != null) {
					actions.AddRange (refactorings);
				} else {
					actions = refactorings;
				}
			}

			return actions;
		}

		SnapshotSpan? TryGetSelectedSpan ()
		{
			Debug.Assert (provider.JoinableTaskContext.IsOnMainThread);

			var spans = textView.Selection.SelectedSpans;
			if (spans.Count == 1) {
				return spans[0];
			}
			return null;
		}

		async Task<SnapshotSpan?> GetSelectedSpanAsync (CancellationToken cancellationToken)
		{
			if (!provider.JoinableTaskContext.IsOnMainThread) {
				await provider.JoinableTaskContext.Factory.SwitchToMainThreadAsync (cancellationToken);
			}
			return TryGetSelectedSpan ();
		}

		public async Task<bool> HasSuggestedActionsAsync (ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken)
		{
			SnapshotSpan? possibleSelection = provider.JoinableTaskContext.IsOnMainThread? TryGetSelectedSpan () : null;

			var result = await parser.GetOrProcessAsync (range.Snapshot, cancellationToken);

			if (requestedActionCategories.Contains (PredefinedSuggestedActionCategoryNames.CodeFix)) {
				if (await provider.CodeFixService.HasFixes (textBuffer, result, range, cancellationToken)) {
					return true;
				}
			}

			if (requestedActionCategories.Contains (PredefinedSuggestedActionCategoryNames.CodeFix)) {
				possibleSelection ??= await GetSelectedSpanAsync (cancellationToken);
				if (possibleSelection is SnapshotSpan selection && await provider.RefactoringService.HasRefactorings (result, selection, cancellationToken)) {
					return true;
				}
			}

			return false;
		}

		public bool TryGetTelemetryId (out Guid telemetryId)
		{
			telemetryId = Guid.Empty;
			return false;
		}

		public void Dispose ()
		{
			var p = parser;
			if (p != null) {
				parser.ParseCompleted -= ParseCompleted;
				parser = null;
			}
		}
	}
}
