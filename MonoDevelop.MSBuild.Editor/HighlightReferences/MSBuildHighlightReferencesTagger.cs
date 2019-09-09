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
using MSBuildRefUsage = MonoDevelop.MSBuild.Language.ReferenceUsage;
using ReferenceUsage = MonoDevelop.Xml.Editor.HighlightReferences.ReferenceUsage;

namespace MonoDevelop.MSBuild.Editor.HighlightReferences
{
	class MSBuildHighlightReferencesTagger : HighlightReferencesTagger
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

		protected override async Task<ImmutableArray<(ReferenceUsage type, SnapshotSpan location)>> GetReferencesAsync (
			SnapshotPoint caretLocation,
			CancellationToken token)
		{
			var snapshot = caretLocation.Snapshot;
			var spineParser = parser.GetSpineParser (caretLocation);
			var textSource = snapshot.GetTextSource ();
			var doc = parser.LastParseResult?.MSBuildDocument;

			var rr = MSBuildResolver.Resolve (spineParser, textSource, doc, provider.FunctionTypeProvider);
			if (rr == null || rr.ReferenceKind == MSBuildReferenceKind.None) {
				return ImmutableArray<(ReferenceUsage type, SnapshotSpan location)>.Empty;
			}

			var parseResult = await parser.GetOrParseAsync ((ITextSnapshot2)snapshot, token);
			doc = parseResult?.MSBuildDocument;
			if (doc == null) {
				return ImmutableArray<(ReferenceUsage type, SnapshotSpan location)>.Empty;
			}

			var collector = MSBuildReferenceCollector.Create (rr, provider.FunctionTypeProvider);

			await Task.Run (() => collector.Run (doc, token: token), token);

			var references = new List<(ReferenceUsage type, SnapshotSpan location)> (collector.Results.Count);

			foreach (var reference in collector.Results) {
				var usage = ConvertUsage (reference.Usage);
				references.Add ((usage, new SnapshotSpan (snapshot, reference.Offset, reference.Length)));
			}

			return references.ToImmutableArray ();
		}

		static ReferenceUsage ConvertUsage (MSBuildRefUsage usage)
		{
			switch (usage) {
			case MSBuildRefUsage.Write:
				return ReferenceUsage.Write;
			case MSBuildRefUsage.Declaration:
				return ReferenceUsage.Definition;
			case MSBuildRefUsage.Read:
				return ReferenceUsage.Read;
			default:
				throw new ArgumentException ($"Unsupported value {usage}");
			}
		}
	}
}
