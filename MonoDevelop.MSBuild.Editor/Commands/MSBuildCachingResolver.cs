// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Threading;

using MonoDevelop.MSBuild.Editor.Completion;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.Xml.Editor;

namespace MonoDevelop.MSBuild.Editor.Commands
{
	/// <summary>
	/// Use to resolve at the current caret position. Results will be shared e.g. by multiple command handlers updating state.
	/// Only usable from UI thread.
	/// </summary>
	[Export, PartCreationPolicy(CreationPolicy.Shared)]
	class MSBuildCachingResolver
	{
		[ImportingConstructor]
		public MSBuildCachingResolver (
			IFunctionTypeProvider functionTypeProvider,
			JoinableTaskContext joinableTaskContext,
			MSBuildParserProvider ParserProvider)
		{
			FunctionTypeProvider = functionTypeProvider;
			JoinableTaskContext = joinableTaskContext;
			this.ParserProvider = ParserProvider;
		}

		public IFunctionTypeProvider FunctionTypeProvider { get; }
		public JoinableTaskContext JoinableTaskContext { get; }
		public MSBuildParserProvider ParserProvider { get; }

		struct CachedResult
		{
			public SnapshotPoint Position;
			public MSBuildRootDocument Doc;
			public MSBuildResolveResult Result;
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
			if (cached.Position == position) {
				doc = cached.Doc;
				rr = cached.Result;
				return true;
			}

			var parser = ParserProvider.GetParser (buffer);
			var lastResult = parser.LastOutput;

			// if it's still at the same position and the last result is the same stale version, there's no point trying again
			if (cached.Position == position && lastResult.MSBuildDocument == cached.Doc) {
				doc = cached.Doc;
				rr = cached.Result;
				return false;
			}

			// actually do the work
			cached.Doc = doc = lastResult.MSBuildDocument;
			cached.Result = rr = MSBuildResolver.Resolve (
				parser.XmlParser.GetSpineParser (position),
				position.Snapshot.GetTextSource (),
				doc, FunctionTypeProvider);
			cached.Position = position;

			this.cached = cached;

			return lastResult.Snapshot == position.Snapshot;
		}
	}
}
