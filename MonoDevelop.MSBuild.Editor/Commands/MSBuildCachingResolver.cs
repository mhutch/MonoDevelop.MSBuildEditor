// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;
using System.Diagnostics;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Threading;

using MonoDevelop.MSBuild.Editor.Completion;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.Xml.Editor.Completion;

namespace MonoDevelop.MSBuild.Editor.Commands
{
	/// <summary>
	/// Use to resolve at the current caret position. Results will be shared e.g. by multiple command handlers updating state.
	/// Only usable from UI thread.
	/// </summary>
	[Export, PartCreationPolicy(CreationPolicy.Shared)]
	class MSBuildCachingResolver
	{
		[Import]
		public IFunctionTypeProvider FunctionTypeProvider { get; set; }

		[Import]
		public JoinableTaskContext JoinableTaskContext { get; set; }

		SnapshotPoint cachedPosition;
		MSBuildRootDocument cachedDoc;
		MSBuildResolveResult cachedResult;
		bool cachedIsUpToDate;

		/// <summary>
		/// Gets a resolved reference from the document. The schema may be stale, in which case it returns false.
		/// </summary>
		public bool GetResolvedReference (ITextBuffer buffer, SnapshotPoint position, out MSBuildRootDocument doc, out MSBuildResolveResult rr)
		{
			Debug.Assert (JoinableTaskContext.IsOnMainThread);

			// if the cached result is up to date, return it
			if (cachedPosition == position && cachedIsUpToDate) {
				doc = cachedDoc;
				rr = cachedResult;
				return true;
			}

			var parser = BackgroundParser<MSBuildParseResult>.GetParser<MSBuildBackgroundParser> ((ITextBuffer2)buffer);
			var lastResult = parser.LastParseResult;

			// if it's still at the same position and the last result is the same stale version, there's no point trying again
			if (cachedPosition == position && lastResult.MSBuildDocument == cachedDoc) {
				doc = cachedDoc;
				rr = cachedResult;
				return false;
			}

			// actually do the work
			cachedDoc = doc = lastResult.MSBuildDocument;
			cachedResult = rr = MSBuildResolver.Resolve (
				parser.GetSpineParser (position),
				position.Snapshot.GetTextSource (),
				doc, FunctionTypeProvider);
			cachedPosition = position;
			return cachedIsUpToDate = lastResult.Snapshot == position.Snapshot;
		}
	}
}
