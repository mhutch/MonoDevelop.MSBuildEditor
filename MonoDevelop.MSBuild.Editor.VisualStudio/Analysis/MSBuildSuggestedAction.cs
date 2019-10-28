// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;

namespace MonoDevelop.MSBuild.Editor.Analysis
{
	internal class MSBuildSuggestedAction : ISuggestedAction
	{
		readonly ITextBuffer buffer;
		readonly MSBuildCodeFix fix;

		public MSBuildSuggestedAction (ITextBuffer buffer, MSBuildCodeFix fix)
		{
			this.buffer = buffer;
			this.fix = fix;
		}

		public bool HasActionSets => false;

		public string DisplayText => fix.Action.Title;

		public ImageMoniker IconMoniker => default;

		public string IconAutomationText => null;

		public string InputGestureText => null;

		public bool HasPreview => false;

		public void Dispose ()
		{
		}

		public Task<IEnumerable<SuggestedActionSet>> GetActionSetsAsync (CancellationToken cancellationToken)
			=> Task.FromResult<IEnumerable<SuggestedActionSet>> (null);

		public Task<object> GetPreviewAsync (CancellationToken cancellationToken)
		{
			throw new NotSupportedException ();
		}

		public void Invoke (CancellationToken cancellationToken)
		{
			var operations = fix.Action.ComputeOperationsAsync (cancellationToken).WaitAndGetResult (cancellationToken);
			foreach (var op in operations) {
				op.Apply (buffer, cancellationToken);
			}
		}

		public bool TryGetTelemetryId (out Guid telemetryId)
		{
			telemetryId = Guid.Empty;
			return false;
		}
	}
}
