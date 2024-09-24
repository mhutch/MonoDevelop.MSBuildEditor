// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

using MonoDevelop.MSBuild.Analysis;
using MonoDevelop.MSBuild.Editor.CodeActions;
using MonoDevelop.MSBuild.Editor.Completion;
using MonoDevelop.Xml.Editor.Parsing;
using MonoDevelop.Xml.Logging;
using MonoDevelop.Xml.Options;

using static Microsoft.CodeAnalysis.Text.Extensions;

namespace MonoDevelop.MSBuild.Editor.Analysis
{
	sealed class MSBuildSuggestedActionSource : ISuggestedActionsSource2
	{
		readonly MSBuildSuggestedActionsSourceProvider provider;
		readonly ITextView textView;
		readonly ITextBuffer textBuffer;
		readonly ILogger logger;
		readonly IOptionsReader options;
		MSBuildBackgroundParser parser;

		public MSBuildSuggestedActionSource (MSBuildSuggestedActionsSourceProvider provider, ITextView textView, ITextBuffer textBuffer, ILogger logger)
		{
			this.provider = provider;
			this.textView = textView;
			this.textBuffer = textBuffer;
			this.logger = logger;
			parser = provider.ParserProvider.GetParser (textBuffer);
			parser.ParseCompleted += ParseCompleted;
			options = new VSEditorOptionsReader (textView.Options);
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
					fix.GetSuggestedActionCategory (),
					[
						provider.SuggestedActionFactory.CreateSuggestedAction (provider.PreviewService, textView, textBuffer, fix)
					]);
			}
		}

		async Task<List<MSBuildCodeAction>> GetSuggestedActionsAsync (ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken)
		{
			// FIXME: do we need to grab the selection or will the caller do it for us?

			// grab selection first as we are on the UI thread at this point, and can avoid switching to it later
			// var possibleSelection = TryGetSelectedSpan ();

			var result = await parser.GetOrProcessAsync (range.Snapshot, cancellationToken);

			var sourceText = range.Snapshot.AsText ();

			var requestedActionKinds = requestedActionCategories.ToCodeActionKinds ();

			var	actions = await provider.CodeActionService.GetCodeActions (sourceText, result.MSBuildDocument, new Xml.Dom.TextSpan(range.Start, range.Length), requestedActionKinds, options, cancellationToken);

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

			var sourceText = range.Snapshot.AsText ();

			var requestedKinds = requestedActionCategories.ToCodeActionKinds ();

			// TODO: VS only cares about the "most severe" category, as it uses this to determine the icon to show in the margin, so we may be able to short circuit if we check for error level severity
			var actions = await provider.CodeActionService.GetCodeActions (sourceText, result.MSBuildDocument, new Xml.Dom.TextSpan (range.Start, range.Length), requestedKinds, options, cancellationToken);

			var categories = actions.Select (a => a.GetSuggestedActionCategory ());

			// assume that this filters out duplicates
			return provider.CategoryRegistry.CreateSuggestedActionCategorySet (categories);
		}
	}

	static class SuggestedActionCategorySetExtensions
	{
		public static ISet<MSBuildCodeActionKind> ToCodeActionKinds (this ISuggestedActionCategorySet categories)
		{
			var result = new HashSet<MSBuildCodeActionKind> ();

			foreach (var category in categories) {
				switch (category) {
				case PredefinedSuggestedActionCategoryNames.Any:
					result.Clear ();
					return result;
				case PredefinedSuggestedActionCategoryNames.Refactoring:
					result.Add (MSBuildCodeActionKind.Refactoring);
					break;
				case PredefinedSuggestedActionCategoryNames.CodeFix:
					result.Add (MSBuildCodeActionKind.CodeFix);
					break;
				case PredefinedSuggestedActionCategoryNames.ErrorFix:
					result.Add (MSBuildCodeActionKind.ErrorFix);
					break;
				case PredefinedSuggestedActionCategoryNames.StyleFix:
					result.Add (MSBuildCodeActionKind.StyleFix);
					break;
				default:
					// TODO: log warning?
					break;
				}
			}

			return result;
		}

		public static string GetSuggestedActionCategory(this MSBuildCodeAction action)
		{
			if (action.GetFixesErrorDiagnostics ()) {
				return PredefinedSuggestedActionCategoryNames.ErrorFix;
			}

			return action.Kind switch {
				MSBuildCodeActionKind.ErrorFix => PredefinedSuggestedActionCategoryNames.ErrorFix,
				MSBuildCodeActionKind.CodeFix => PredefinedSuggestedActionCategoryNames.CodeFix,
				MSBuildCodeActionKind.StyleFix => PredefinedSuggestedActionCategoryNames.StyleFix,
				MSBuildCodeActionKind.Refactoring => PredefinedSuggestedActionCategoryNames.Refactoring,
				MSBuildCodeActionKind.RefactoringExtract => PredefinedSuggestedActionCategoryNames.Refactoring,
				MSBuildCodeActionKind.RefactoringInline => PredefinedSuggestedActionCategoryNames.Refactoring,
				_ => action.FixesDiagnostics.Count > 0
					? PredefinedSuggestedActionCategoryNames.CodeFix
					: PredefinedSuggestedActionCategoryNames.Refactoring
			};
		}
	}
}
