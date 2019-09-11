// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using MonoDevelop.MSBuild.Editor.Completion;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.Xml.Editor.HighlightReferences;
using MonoDevelop.Xml.Editor.Tags;

namespace MonoDevelop.MSBuild.Editor.HighlightReferences
{
	class MSBuildHighlightReferencesTagger : HighlightTagger<NavigableHighlightTag, ReferenceUsage>
	{
		readonly MSBuildBackgroundParser parser;
		readonly MSBuildHighlightReferencesTaggerProvider provider;

		public MSBuildHighlightReferencesTagger (
			ITextView textView,
			MSBuildHighlightReferencesTaggerProvider provider
			)
			: base (textView, provider.JoinableTaskContext)
		{
			parser = MSBuildBackgroundParser.GetParser (textView.TextBuffer);
			this.provider = provider;
		}

		protected override bool RemainsValidIfCaretMovesBetweenHighlights => true;

		protected async override
			Task<(SnapshotSpan sourceSpan, ImmutableArray<(ReferenceUsage kind, SnapshotSpan location)> highlights)>
			GetHighlightsAsync (SnapshotPoint caretLocation, CancellationToken token)
		{
			// parser is not currently threadsafe
			await JoinableTaskContext.Factory.SwitchToMainThreadAsync (token);

			var snapshot = caretLocation.Snapshot;
			var spineParser = parser.GetSpineParser (caretLocation);
			var textSource = snapshot.GetTextSource ();
			var doc = parser.LastParseResult?.MSBuildDocument;
			if (doc == null) {
				return Empty;
			}

			var rr = MSBuildResolver.Resolve (spineParser, textSource, doc, provider.FunctionTypeProvider, token);
			if (!MSBuildReferenceCollector.CanCreate (rr)) {
				return Empty;
			}

			var parseResult = await parser.GetOrParseAsync ((ITextSnapshot2)snapshot, token).ConfigureAwait (false);
			doc = parseResult?.MSBuildDocument;
			if (doc == null) {
				return Empty;
			}

			var collector = MSBuildReferenceCollector.Create (rr, provider.FunctionTypeProvider);

			await Task.Run (() => collector.Run (doc, token: token), token);

			var references = new List<(ReferenceUsage type, SnapshotSpan location)> (collector.Results.Count);

			foreach (var reference in collector.Results) {
				references.Add ((reference.Usage, new SnapshotSpan (snapshot, reference.Offset, reference.Length)));
			}

			return (
				new SnapshotSpan (caretLocation.Snapshot, rr.ReferenceOffset, rr.ReferenceLength),
				references.ToImmutableArray ());
		}

		protected override NavigableHighlightTag GetTag (ReferenceUsage kind)
		{
			switch (kind) {
			case ReferenceUsage.Write:
				return WrittenReferenceHighlightTag.Instance;
			case ReferenceUsage.Declaration:
				return DefinitionHighlightTag.Instance;
			case ReferenceUsage.Read:
				return ReferenceHighlightTag.Instance;
			default:
				throw new ArgumentException ($"Unsupported value {kind}");
			}
		}
	}
}
