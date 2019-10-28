// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;

using MonoDevelop.MSBuild.Editor.Completion;

namespace MonoDevelop.MSBuild.Editor.Analysis
{
	sealed class MSBuildSuggestedActionSource : ISuggestedActionsSource
	{
		readonly MSBuildSuggestedActionsSourceProvider provider;
		readonly ITextView textView;
		readonly ITextBuffer textBuffer;
		readonly MSBuildBackgroundParser parser;

		public MSBuildSuggestedActionSource (MSBuildSuggestedActionsSourceProvider provider, ITextView textView, ITextBuffer textBuffer)
		{
			this.provider = provider;
			this.textView = textView;
			this.textBuffer = textBuffer;

			parser = provider.ParserProvider.GetParser (textBuffer);		}

		void TagsChanged (object sender, TagsChangedEventArgs e)
		{
			SuggestedActionsChanged?.Invoke (this, EventArgs.Empty);
		}

		public event EventHandler<EventArgs> SuggestedActionsChanged;

		public IEnumerable<SuggestedActionSet> GetSuggestedActions (ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken)
		{
			var fixes = GetCodeFixesAsync (range, cancellationToken).WaitAndGetResult (cancellationToken);
			foreach (var fix in fixes) {
				yield return new SuggestedActionSet (new MSBuildSuggestedAction[] { new MSBuildSuggestedAction (textBuffer, fix) });
			}
		}

		async Task<List<MSBuildCodeFix>> GetCodeFixesAsync (SnapshotSpan range, CancellationToken cancellationToken)
		{
			var result = await parser.GetOrProcessAsync (range.Snapshot, cancellationToken);
			return await provider.CodeFixService.GetFixes (result, range, cancellationToken);
		}

		public async Task<bool> HasSuggestedActionsAsync (ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken)
		{
			var result = await parser.GetOrProcessAsync (range.Snapshot, cancellationToken);
			return await provider.CodeFixService.HasFixes (result, range, cancellationToken);
		}

		public bool TryGetTelemetryId (out Guid telemetryId)
		{
			telemetryId = Guid.Empty;
			return false;
		}

		public void Dispose ()
		{
		}
	}
}
