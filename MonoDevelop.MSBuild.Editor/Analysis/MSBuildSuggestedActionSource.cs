// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
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
			var fixes = GetCodeFixesAsync (range, cancellationToken).WaitAndGetResult (cancellationToken);
			foreach (var fix in fixes) {
				yield return new SuggestedActionSet (
					null,
					new ISuggestedAction[] {
						provider.SuggestedActionFactory.CreateSuggestedAction (provider.PreviewService, textBuffer, fix)
					});
			}
		}

		async Task<List<MSBuildCodeFix>> GetCodeFixesAsync (SnapshotSpan range, CancellationToken cancellationToken)
		{
			var result = await parser.GetOrProcessAsync (range.Snapshot, cancellationToken);

			var fixes = await provider.CodeFixService.GetFixes (textBuffer, result, range, cancellationToken);
			var refactorings = await provider.RefactoringService.GetRefactorings (result, range, cancellationToken);

			fixes.AddRange (refactorings);
			return fixes;
		}

		public async Task<bool> HasSuggestedActionsAsync (ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken)
		{
			var result = await parser.GetOrProcessAsync (range.Snapshot, cancellationToken);
			if (await provider.CodeFixService.HasFixes (textBuffer, result, range, cancellationToken)) {
				return true;
			}

			if (await provider.RefactoringService.HasRefactorings (result,  range, cancellationToken)) {
				return true;
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
