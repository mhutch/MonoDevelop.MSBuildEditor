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

		struct CachedResult
		{
			public SnapshotPoint Position;
			public MSBuildRootDocument Doc;
			public MSBuildResolveResult Result;
			public bool IsUpToDate;
		}

		CachedResult cached;

		/// <summary>
		/// Gets a resolved reference from the document. The schema may be stale, in which case it returns false.
		/// </summary>
		public bool GetResolvedReference (ITextBuffer buffer, SnapshotPoint position, out MSBuildRootDocument doc, out MSBuildResolveResult rr)
		{
			// grab the field into a local to make this thread safe
			var cached = this.cached;

			// if the cached result is up to date, return it
			if (cached.Position == position && cached.IsUpToDate) {
				doc = cached.Doc;
				rr = cached.Result;
				return true;
			}

			var parser = BackgroundParser<MSBuildParseResult>.GetParser<MSBuildBackgroundParser> ((ITextBuffer2)buffer);
			var lastResult = parser.LastParseResult;

			// if it's still at the same position and the last result is the same stale version, there's no point trying again
			if (cached.Position == position && lastResult.MSBuildDocument == cached.Doc) {
				doc = cached.Doc;
				rr = cached.Result;
				return false;
			}

			// actually do the work
			cached.Doc = doc = lastResult.MSBuildDocument;
			cached.Result = rr = MSBuildResolver.Resolve (
				parser.GetSpineParser (position),
				position.Snapshot.GetTextSource (),
				doc, FunctionTypeProvider);
			cached.Position = position;

			this.cached = cached;

			return cached.IsUpToDate = lastResult.Snapshot == position.Snapshot;
		}
	}
}
