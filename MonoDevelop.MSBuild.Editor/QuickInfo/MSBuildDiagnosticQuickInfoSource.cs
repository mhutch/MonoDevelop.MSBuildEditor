// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;

using MonoDevelop.Xml.Logging;

namespace MonoDevelop.MSBuild.Editor.QuickInfo;

/// <summary>
/// Display custom tooltips for diagnostic tags, rather than putting elements on the tags, so we can avoid creating them unless needed
/// </summary>
partial class MSBuildDiagnosticQuickInfoSource : IAsyncQuickInfoSource
{
	readonly ITextBuffer textBuffer;
	readonly ILogger logger;
	readonly ITagAggregator<MSBuildDiagnosticTag> tagAggregator;
	readonly DisplayElementFactory displayElementFactory;

	public MSBuildDiagnosticQuickInfoSource (ITextBuffer textBuffer, ILogger logger, ITagAggregator<MSBuildDiagnosticTag> tagAggregator, DisplayElementFactory displayElementFactory)
	{
		this.textBuffer = textBuffer;
		this.logger = logger;
		this.tagAggregator = tagAggregator;
		this.displayElementFactory = displayElementFactory;
	}

	public Task<QuickInfoItem> GetQuickInfoItemAsync (IAsyncQuickInfoSession session, CancellationToken cancellationToken)
		=> logger.InvokeAndLogExceptions (() => Task.FromResult (GetQuickInfoItemInternal (session, cancellationToken)));

	QuickInfoItem GetQuickInfoItemInternal (IAsyncQuickInfoSession session, CancellationToken cancellationToken)
	{
		ITextSnapshot snapshot = textBuffer.CurrentSnapshot;
		SnapshotPoint? triggerPoint = session.GetTriggerPoint (snapshot);
		if (!triggerPoint.HasValue) {
			return null;
		}

		SnapshotSpan triggerSpan = new (triggerPoint.Value.Snapshot, triggerPoint.Value.Position, 0);
		var tagMappingSpans = tagAggregator.GetTags (triggerSpan).ToList ();

		if (tagMappingSpans.Count == 0) {
			return null;
		}

		var elements = new List<object> ();
		SnapshotSpan? applicableToSpan = null;

		foreach (var mappingSpan in tagMappingSpans) {
			var diagnostic = mappingSpan.Tag.Diagnostic;
			elements.Add (displayElementFactory.GetDiagnosticTooltip (diagnostic));

			var tagSpan = mappingSpan.Span.GetSpans (snapshot).First ();
			applicableToSpan = applicableToSpan.HasValue ? applicableToSpan.Value.Overlap (tagSpan) : tagSpan;
		}

		return new QuickInfoItem (
			snapshot.CreateTrackingSpan (applicableToSpan.Value, SpanTrackingMode.EdgeExclusive),
			new ContainerElement (ContainerElementStyle.Stacked | ContainerElementStyle.VerticalPadding, elements)
		);
	}

	public void Dispose ()
	{
	}
}
