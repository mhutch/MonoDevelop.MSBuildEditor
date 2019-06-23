// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Threading;

using MonoDevelop.MSBuild.Editor.Completion;
using MonoDevelop.Xml.Editor.Completion;
using MonoDevelop.Xml.Parser;

namespace MonoDevelop.MSBuild.Editor
{
	class MSBuildValidationTagger : ITagger<IErrorTag>, IDisposable
	{
		readonly MSBuildBackgroundParser parser;
		readonly JoinableTaskContext joinableTaskContext;
		ParseCompletedEventArgs<MSBuildParseResult> lastArgs;

		public MSBuildValidationTagger (ITextBuffer buffer, JoinableTaskContext joinableTaskContext)
		{
			parser = BackgroundParser<MSBuildParseResult>.GetParser<MSBuildBackgroundParser> ((ITextBuffer2)buffer);
			parser.ParseCompleted += ParseCompleted;
			this.joinableTaskContext = joinableTaskContext;
		}

		void ParseCompleted (object sender, ParseCompletedEventArgs<MSBuildParseResult> args)
		{
			lastArgs = args;

			joinableTaskContext.Factory.Run (async delegate {
				await joinableTaskContext.Factory.SwitchToMainThreadAsync ();
				//FIXME: figure out which spans changed, if any, and only invalidate those
				TagsChanged?.Invoke (this, new SnapshotSpanEventArgs (new SnapshotSpan (args.Snapshot, 0, args.Snapshot.Length)));
			});
		}

		public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

		public void Dispose ()
		{
			parser.ParseCompleted -= ParseCompleted;
		}

		public IEnumerable<ITagSpan<IErrorTag>> GetTags (NormalizedSnapshotSpanCollection spans)
		{
			//this may be assigned from another thread so capture a consistent value
			var args = lastArgs;

			if (args == null || spans.Count == 0 || spans[0].IsEmpty)
				yield break;

			var parse = args.ParseResult;
			var snapshot = args.Snapshot;

			//FIXME how do errors that span multiple spans work?
			foreach (var taggingSpan in spans) {
				foreach (var diag in parse.Diagnostics) {
					var diagSpan = diag.Span;
					if (diagSpan.Start >= taggingSpan.Start && diagSpan.Start <= taggingSpan.End) {
						yield return CreateErrorTag (diag, snapshot);
					}
				}
			}
		}

		TagSpan<ErrorTag> CreateErrorTag (XmlDiagnosticInfo diag, ITextSnapshot snapshot)
		{
			var errorType = GetErrorTypeName (diag.Severity);
			var span = new SnapshotSpan (snapshot, diag.Span.Start, diag.Span.Length);
			return new TagSpan<ErrorTag> (span, new ErrorTag (errorType, diag.Message));
		}

		static string GetErrorTypeName (DiagnosticSeverity severity)
		{
			switch (severity) {
			case DiagnosticSeverity.Error:
				return PredefinedErrorTypeNames.SyntaxError;
			case DiagnosticSeverity.Warning:
				return PredefinedErrorTypeNames.Warning;
			}
			throw new ArgumentException ($"Unknown DiagnosticSeverity value {severity}", nameof (severity));
		}
	}
}
