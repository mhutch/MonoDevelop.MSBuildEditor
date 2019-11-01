// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Differencing;
using Microsoft.VisualStudio.Utilities;

using MonoDevelop.MSBuild.Analysis;

namespace MonoDevelop.MSBuild.Editor
{
	interface IDifferenceViewElementFactory
	{
		Task<object> CreateDiffViewElementAsync (IDifferenceBuffer diffBuffer);
	}

	/// <summary>
	/// Creates a preview window for showing the difference that will occur when changes are 
	/// applied.
	/// </summary>
	[Export (typeof (PreviewChangesService))]
	class PreviewChangesService
	{
		private readonly IDifferenceViewElementFactory _differenceViewfactory;
		private readonly IDifferenceBufferFactoryService _diffBufferFactory;
		private readonly ITextBufferFactoryService _bufferFactory;

		[ImportingConstructor]
		public PreviewChangesService (
			IDifferenceViewElementFactory differenceViewfactory,
			IDifferenceBufferFactoryService diffBufferFactory,
			ITextBufferFactoryService bufferFactory)
		{
			_differenceViewfactory = differenceViewfactory;
			_diffBufferFactory = diffBufferFactory;
			_bufferFactory = bufferFactory;
		}

		public async Task<object> CreateDiffViewAsync (IEnumerable<MSBuildActionOperation> actions, ITextSnapshot snapshot, IContentType contentType, CancellationToken cancellationToken)
		{
			// Create a copy of the left hand buffer (we're going to remove all of the
			// content we don't care about from it).
			var leftBuffer = _bufferFactory.CreateTextBuffer (contentType);
			using (var edit = leftBuffer.CreateEdit ()) {
				edit.Insert (0, snapshot.GetText ());
				edit.Apply ();
			}

			// create a buffer for the right hand side, copy the original buffer
			// into it, and then apply the changes.
			var rightBuffer = _bufferFactory.CreateTextBuffer (contentType);
			using (var edit = rightBuffer.CreateEdit ()) {
				edit.Insert (0, snapshot.GetText ());
				edit.Apply ();
			}

			var startingVersion = rightBuffer.CurrentSnapshot;

			foreach (var action in actions) {
				action.Apply (rightBuffer, cancellationToken);
			}

			var textChanges = startingVersion.Version.Changes;
			int minPos = startingVersion.Length, maxPos = 0;
			foreach (var change in textChanges) {
				minPos = Math.Min (change.OldPosition, minPos);
				maxPos = Math.Max (change.OldPosition, maxPos);
			}

			if (minPos == startingVersion.Length && maxPos == 0) {
				// no changes?  that's weird...
				return null;
			}

			MinimizeBuffers (leftBuffer, rightBuffer, startingVersion, minPos, maxPos);

			// create the difference buffer and view...
			var diffBuffer = _diffBufferFactory.CreateDifferenceBuffer (leftBuffer, rightBuffer);

			var diffView = await _differenceViewfactory.CreateDiffViewElementAsync (diffBuffer).ConfigureAwait (true);

			return diffView;
		}

		private static void MinimizeBuffers (ITextBuffer leftBuffer, ITextBuffer rightBuffer, ITextSnapshot startingVersion, int minPos, int maxPos)
		{
			// Remove the unchanged content from both buffers
			using (var edit = leftBuffer.CreateEdit ()) {
				edit.Delete (0, minPos);
				edit.Delete (Span.FromBounds (maxPos, startingVersion.Length));
				edit.Apply ();
			}

			using (var edit = rightBuffer.CreateEdit ()) {
				edit.Delete (
					0,
					Tracking.TrackPositionForwardInTime (
						PointTrackingMode.Negative,
						minPos,
						startingVersion.Version,
						rightBuffer.CurrentSnapshot.Version
					)
				);

				edit.Delete (
					Span.FromBounds (
						Tracking.TrackPositionForwardInTime (
							PointTrackingMode.Positive,
							maxPos,
							startingVersion.Version,
							rightBuffer.CurrentSnapshot.Version
						),
						rightBuffer.CurrentSnapshot.Length
					)
				);
				edit.Apply ();
			}
		}
	}
}