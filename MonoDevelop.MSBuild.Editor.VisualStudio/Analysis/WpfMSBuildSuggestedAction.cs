// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

using MonoDevelop.MSBuild.Editor.CodeActions;

namespace MonoDevelop.MSBuild.Editor.Analysis
{
	[Export (typeof (IMSBuildSuggestedActionFactory))]
	class WpfMSBuildSuggestedActionFactory : IMSBuildSuggestedActionFactory
	{
		public ISuggestedAction CreateSuggestedAction (PreviewChangesService previewService, ITextView textView, ITextBuffer buffer, MSBuildCodeAction action)
			=> new WpfMSBuildSuggestedAction (previewService, textView, buffer, action);
	}

	class WpfMSBuildSuggestedAction : ISuggestedAction
	{
		readonly PreviewChangesService previewService;
		readonly ITextView textView;
		readonly ITextBuffer buffer;
		readonly MSBuildCodeAction action;

		public WpfMSBuildSuggestedAction (PreviewChangesService previewService, ITextView textView, ITextBuffer buffer, MSBuildCodeAction action)
		{
			this.previewService = previewService;
			this.textView = textView;
			this.buffer = buffer;
			this.action = action;
		}

		public bool HasActionSets => false;

		public string DisplayText => action.Title;

		public ImageMoniker IconMoniker => default;

		public string IconAutomationText => null;

		public string InputGestureText => null;

		public bool HasPreview => previewService != null;

		public void Dispose ()
		{
		}

		public Task<IEnumerable<SuggestedActionSet>> GetActionSetsAsync (CancellationToken cancellationToken)
			=> Task.FromResult<IEnumerable<SuggestedActionSet>> (null);

		public async Task<object> GetPreviewAsync (CancellationToken cancellationToken)
		{
			var workspaceEdit = await action.ComputeOperationsAsync (cancellationToken).ConfigureAwait (true);

			// TODO: show changes that affect other files than this one
			var documentEdits = workspaceEdit.GetDocumentEdits (buffer);

			var allTextEdits = documentEdits.SelectMany (e => e.TextEdits).ToList();

			return previewService.CreateDiffViewAsync (
				allTextEdits,
				textView.Options,
				buffer.CurrentSnapshot, buffer.ContentType, cancellationToken
			);
		}

		public void Invoke (CancellationToken cancellationToken)
		{
			var workspaceEdit = action.ComputeOperationsAsync (cancellationToken).WaitAndGetResult (cancellationToken);
			workspaceEdit.Apply(buffer, cancellationToken, textView);
		}

		public bool TryGetTelemetryId (out Guid telemetryId)
		{
			telemetryId = Guid.Empty;
			return false;
		}
	}
}
