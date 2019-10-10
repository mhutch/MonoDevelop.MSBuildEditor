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
using MonoDevelop.Xml.Editor;
using MonoDevelop.Xml.Editor.Completion;
using MonoDevelop.Xml.Editor.HighlightReferences;
using MonoDevelop.Xml.Editor.Tagging;

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
			parser = provider.ParserProvider.GetParser (textView.TextBuffer);
			this.provider = provider;
		}

		protected override bool RemainsValidIfCaretMovesBetweenHighlights => true;

		protected async override
			Task<(SnapshotSpan sourceSpan, ImmutableArray<(ReferenceUsage kind, SnapshotSpan location)> highlights)>
			GetHighlightsAsync (SnapshotPoint caretLocation, CancellationToken token)
		{
			var snapshot = caretLocation.Snapshot;
			var spineParser = parser.XmlParser.GetSpineParser (caretLocation);
			var textSource = snapshot.GetTextSource ();
			var doc = parser.LastOutput?.MSBuildDocument;
			if (doc == null) {
				return Empty;
			}

			var rr = MSBuildResolver.Resolve (spineParser, textSource, doc, provider.FunctionTypeProvider, token);
			if (!MSBuildReferenceCollector.CanCreate (rr)) {
				return Empty;
			}

			var parseResult = await parser.GetOrProcessAsync (snapshot, token).ConfigureAwait (false);
			doc = parseResult?.MSBuildDocument;
			if (doc == null) {
				return Empty;
			}

			var references = new List<(ReferenceUsage usage, SnapshotSpan span)> ();
			var collector = MSBuildReferenceCollector.Create (
				rr, provider.FunctionTypeProvider,
				r => references.Add ((r.Usage, new SnapshotSpan (snapshot, r.Offset, r.Length))));

			await Task.Run (() => collector.Run (doc, token: token), token);

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
