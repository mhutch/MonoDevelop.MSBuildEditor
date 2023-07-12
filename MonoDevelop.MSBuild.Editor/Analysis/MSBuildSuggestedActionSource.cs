// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

using MonoDevelop.MSBuild.Analysis;
using MonoDevelop.MSBuild.Editor.Completion;
using MonoDevelop.Xml.Editor.Parsing;
using MonoDevelop.Xml.Logging;

namespace MonoDevelop.MSBuild.Editor.Analysis
{
	sealed class MSBuildSuggestedActionSource : ISuggestedActionsSource2
	{
		readonly MSBuildSuggestedActionsSourceProvider provider;
		readonly ITextView textView;
		readonly ITextBuffer textBuffer;
		readonly ILogger logger;
		MSBuildBackgroundParser parser;

		public MSBuildSuggestedActionSource (MSBuildSuggestedActionsSourceProvider provider, ITextView textView, ITextBuffer textBuffer, ILogger logger)
		{
			this.provider = provider;
			this.textView = textView;
			this.textBuffer = textBuffer;
			this.logger = logger;
			parser = provider.ParserProvider.GetParser (textBuffer);
			parser.ParseCompleted += ParseCompleted;
		}

		void ParseCompleted (object sender, ParseCompletedEventArgs<MSBuildParseResult> e)
		{
			SuggestedActionsChanged?.Invoke (this, EventArgs.Empty);
		}

		public event EventHandler<EventArgs> SuggestedActionsChanged;

		public IEnumerable<SuggestedActionSet> GetSuggestedActions (ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken)
			=> logger.InvokeAndLogExceptions (() => GetSuggestedActionsInternal (requestedActionCategories, range, cancellationToken));

		IEnumerable<SuggestedActionSet> GetSuggestedActionsInternal (ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken)
		{
			var fixes = GetSuggestedActionsAsync (requestedActionCategories, range, cancellationToken).WaitAndGetResult (cancellationToken);
			if (fixes == null) {
				yield break;
			}

			foreach (var fix in fixes) {
				yield return new SuggestedActionSet (
					fix.Category,
					new ISuggestedAction[] {
						provider.SuggestedActionFactory.CreateSuggestedAction (provider.PreviewService, textView, textBuffer, fix)
					});
			}
		}

		async Task<List<MSBuildCodeFix>> GetSuggestedActionsAsync (ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken)
		{
			// grab selection first as we are on the UI thread at this point, and can avoid switching to it later
			var possibleSelection = TryGetSelectedSpan ();

			var result = await parser.GetOrProcessAsync (range.Snapshot, cancellationToken);

			List<MSBuildCodeFix> actions = null;

			var severities = CategoriesToSeverity (requestedActionCategories);
			if (severities != 0) {
				actions = await provider.CodeFixService.GetFixes (textBuffer, result, range, severities, cancellationToken);
				for (int i = 0; i < actions.Count; i++) {
					if (!requestedActionCategories.Contains (actions[i].Category)) {
						actions.RemoveAt (i);
						i--;
					}
				}
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

		static MSBuildDiagnosticSeverity CategoriesToSeverity  (ISuggestedActionCategorySet categories)
		{
			var severity = MSBuildDiagnosticSeverity.None;
			if (categories.Contains (PredefinedSuggestedActionCategoryNames.ErrorFix)) {
				severity |= MSBuildDiagnosticSeverity.Error;
			}
			if (categories.Contains (PredefinedSuggestedActionCategoryNames.CodeFix)) {
				severity |= MSBuildDiagnosticSeverity.Suggestion | MSBuildDiagnosticSeverity.Warning;
			}
			return severity;
		}


		public Task<bool> HasSuggestedActionsAsync (ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken)
			=> logger.InvokeAndLogExceptions (() => HasSuggestedActionsAsyncInternal (requestedActionCategories, range, cancellationToken));

		async Task<bool> HasSuggestedActionsAsyncInternal (ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken)
		{
			var cats = await GetSuggestedActionCategoriesAsync (requestedActionCategories, range, cancellationToken);
			return cats?.Contains (PredefinedSuggestedActionCategoryNames.Any) ?? false;
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

		public Task<ISuggestedActionCategorySet> GetSuggestedActionCategoriesAsync (ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken)
			=> logger.InvokeAndLogExceptions (() => GetSuggestedActionCategoriesAsyncInternal (requestedActionCategories, range, cancellationToken));

		async Task<ISuggestedActionCategorySet> GetSuggestedActionCategoriesAsyncInternal (ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken)
		{
			SnapshotSpan? possibleSelection = provider.JoinableTaskContext.IsOnMainThread ? TryGetSelectedSpan () : null;

			var result = await parser.GetOrProcessAsync (range.Snapshot, cancellationToken);

			var categories = new List<string> ();

			var requestedSeverities = CategoriesToSeverity (requestedActionCategories);
			if (requestedSeverities != 0) {
				var severities = await provider.CodeFixService.GetFixSeverity (textBuffer, result, range, requestedSeverities, cancellationToken);
				if ((severities & MSBuildDiagnosticSeverity.Error) != 0) {
					categories.Add (PredefinedSuggestedActionCategoryNames.ErrorFix);
				}
				if ((severities & (MSBuildDiagnosticSeverity.Warning | MSBuildDiagnosticSeverity.Suggestion)) != 0) {
					categories.Add (PredefinedSuggestedActionCategoryNames.CodeFix);
				}
			}

			if (requestedActionCategories.Contains (PredefinedSuggestedActionCategoryNames.Refactoring)) {
				possibleSelection ??= await GetSelectedSpanAsync (cancellationToken);
				if (possibleSelection is SnapshotSpan selection && await provider.RefactoringService.HasRefactorings (result, selection, cancellationToken)) {
					categories.Add (PredefinedSuggestedActionCategoryNames.Refactoring);
				}
			}

			return provider.CategoryRegistry.CreateSuggestedActionCategorySet (categories);
		}
	}
}
