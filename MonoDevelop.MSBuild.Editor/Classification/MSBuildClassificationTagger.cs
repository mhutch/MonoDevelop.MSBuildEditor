// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Threading;

using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Editor.Parsing;
using MonoDevelop.Xml.Logging;
using MonoDevelop.Xml.Parser;

namespace MonoDevelop.MSBuild.Editor.Classification
{
	/// <summary>
	/// Classifies MSBuild files using the extension's own XML and expression parsers.
	/// Used as a fallback when the VS TextMate service is unavailable (e.g. VS 2026, issue #279).
	/// XML constructs are classified to match Visual Studio's built-in XML editor, and MSBuild
	/// expressions get additional classifications on top.
	/// </summary>
	sealed partial class MSBuildClassificationTagger : ITagger<ClassificationTag>, IDisposable
	{
		const string commentPrefix = "<!--";
		const string commentSuffix = "-->";
		const string cdataPrefix = "<![CDATA[";
		const string cdataSuffix = "]]>";

		readonly ITextBuffer buffer;
		readonly XmlBackgroundParser parser;
		readonly MSBuildClassificationTypeMap typeMap;
		readonly JoinableTaskContext joinableTaskContext;
		readonly ILogger logger;

		bool isDisposed;

		/// <summary>
		/// Creates a classification tagger for the buffer.
		/// </summary>
		/// <param name="buffer">The text buffer to tag.</param>
		/// <param name="parserProvider">Provider used to obtain the per-buffer XML background parser.</param>
		/// <param name="typeMap">Shared map from MSBuild syntax constructs to classification tags.</param>
		/// <param name="joinableTaskContext">Used to raise <see cref="TagsChanged"/> on the main thread.</param>
		/// <param name="logger">Logger for exception reporting.</param>
		public MSBuildClassificationTagger (ITextBuffer buffer, XmlParserProvider parserProvider, MSBuildClassificationTypeMap typeMap, JoinableTaskContext joinableTaskContext, ILogger logger)
		{
			this.buffer = buffer;
			this.typeMap = typeMap;
			this.joinableTaskContext = joinableTaskContext;
			this.logger = logger;

			parser = parserProvider.GetParser (buffer);
			parser.ParseCompleted += ParseCompleted;
			buffer.ContentTypeChanged += BufferContentTypeChanged;
		}

		public event EventHandler<SnapshotSpanEventArgs>? TagsChanged;

		void ParseCompleted (object? sender, ParseCompletedEventArgs<XmlParseResult> args)
		{
			joinableTaskContext.Factory.Run (async delegate {
				await joinableTaskContext.Factory.SwitchToMainThreadAsync ();
				//FIXME: figure out which spans changed, if any, and only invalidate those
				TagsChanged?.Invoke (this, new SnapshotSpanEventArgs (new SnapshotSpan (args.Snapshot, 0, args.Snapshot.Length)));
			});
		}

		void RaiseTagsChanged ()
		{
			ITextSnapshot snapshot = buffer.CurrentSnapshot;
			TagsChanged?.Invoke (this, new SnapshotSpanEventArgs (new SnapshotSpan (snapshot, 0, snapshot.Length)));
		}

		void BufferContentTypeChanged (object? sender, ContentTypeChangedEventArgs e)
		{
			// if the buffer is no longer an MSBuild buffer, discard the tagger.
			// it will be recreated if needed anyway.
			if (!e.AfterContentType.IsOfType (MSBuildContentType.Name)) {
				Dispose ();
			}
		}

		public void Dispose ()
		{
			if (isDisposed) {
				return;
			}
			isDisposed = true;
			parser.ParseCompleted -= ParseCompleted;
			buffer.ContentTypeChanged -= BufferContentTypeChanged;
			buffer.Properties.RemoveProperty (typeof (MSBuildClassificationTagger));
		}

		/// <summary>
		/// Computes classification tags for the requested snapshot spans from the most recent XML parse result.
		/// </summary>
		/// <param name="spans">The spans for which tags are requested.</param>
		/// <returns>Classification tag spans intersecting the requested spans.</returns>
		public IEnumerable<ITagSpan<ClassificationTag>> GetTags (NormalizedSnapshotSpanCollection spans)
			=> logger.InvokeAndLogExceptions (() => GetTagsInternal (spans));

		IEnumerable<ITagSpan<ClassificationTag>> GetTagsInternal (NormalizedSnapshotSpanCollection spans)
		{
			List<ITagSpan<ClassificationTag>> results = new ();

			if (spans.Count == 0) {
				return results;
			}

			ITextSnapshot targetSnapshot = spans[0].Snapshot;

			Task<XmlParseResult> parseTask = parser.GetOrProcessAsync (targetSnapshot, CancellationToken.None);

			XmlParseResult? parseResult;
			if (parseTask.IsCompleted) {
				#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
				parseResult = parseTask.Result;
				#pragma warning restore VSTHRD002
			} else {
				// use the most recent completed parse for now, and raise TagsChanged when the parse
				// for the requested snapshot completes so the tags get recomputed
				parseTask.ContinueWith (t => RaiseTagsChanged (), TaskScheduler.Default).LogTaskExceptionsAndForget (logger);
				parseResult = parser.LastOutput;
			}

			if (parseResult is null) {
				return results;
			}

			ITextSnapshot parseSnapshot = parseResult.TextSnapshot;
			List<ClassificationRun> runs = new ();

			foreach (SnapshotSpan taggingSpan in spans) {
				runs.Clear ();

				// the parse may be from an older snapshot, so clamp the requested range to its length.
				// the tag spans will be mapped back to the requested snapshot below.
				int rangeStart = Math.Min (taggingSpan.Start.Position, parseSnapshot.Length);
				int rangeEnd = Math.Min (taggingSpan.End.Position, parseSnapshot.Length);
				TextSpan range = TextSpan.FromBounds (rangeStart, rangeEnd);

				CollectRuns (parseResult.XDocument, range, parseSnapshot, runs);

				foreach (ClassificationRun run in runs) {
					if (run.Length == 0 || run.Start < 0 || run.End > parseSnapshot.Length) {
						continue;
					}

					SnapshotSpan runSpan = new SnapshotSpan (parseSnapshot, run.Start, run.Length);

					// if the parse was from an older snapshot, map the positions into the requested snapshot.
					// EdgeExclusive means freshly typed characters don't inherit stale classifications.
					if (parseSnapshot != targetSnapshot) {
						ITrackingSpan trackingSpan = parseSnapshot.CreateTrackingSpan (runSpan, SpanTrackingMode.EdgeExclusive);
						runSpan = trackingSpan.GetSpan (targetSnapshot);
						if (runSpan.Length == 0) {
							continue;
						}
					}

					if (runSpan.IntersectsWith (taggingSpan)) {
						results.Add (new TagSpan<ClassificationTag> (runSpan, run.Tag));
					}
				}
			}

			return results;
		}

		/// <summary>
		/// Collects classification runs for all nodes in the container that intersect the range.
		/// </summary>
		/// <param name="container">The XML container whose child nodes are classified.</param>
		/// <param name="range">The range for which runs are requested, in parse snapshot coordinates.</param>
		/// <param name="snapshot">The snapshot the parse result was computed from.</param>
		/// <param name="runs">The list to which runs are added.</param>
		void CollectRuns (XContainer container, TextSpan range, ITextSnapshot snapshot, List<ClassificationRun> runs)
		{
			foreach (XNode node in container.Nodes) {
				if (node.OuterSpan.End < range.Start) {
					continue;
				}
				if (node.OuterSpan.Start >= range.End) {
					break;
				}

				switch (node) {
				case XElement element:
					CollectElementRuns (element, range, snapshot, runs);
					break;
				case XComment comment:
					AddDelimitedRuns (comment.Span, commentPrefix, commentSuffix, typeMap.Comment, snapshot, runs);
					break;
				case XCData cdata:
					AddDelimitedRuns (cdata.Span, cdataPrefix, cdataSuffix, typeMap.CDataSection, snapshot, runs);
					break;
				case XProcessingInstruction processingInstruction:
					CollectProcessingInstructionRuns (processingInstruction, snapshot, runs);
					break;
				case XDocType docType:
					// doctypes are practically nonexistent in MSBuild files, don't bother splitting out the delimiters
					runs.Add (new ClassificationRun (docType.Span, typeMap.ProcessingInstruction));
					break;
				case XClosingTag closingTag:
					// orphaned closing tag with no matching element
					CollectClosingTagRuns (closingTag, snapshot, runs);
					break;
				case XText text:
					AddValueRuns (text.Text, text.Span.Start, typeMap.Text, runs);
					break;
				}
			}
		}

		/// <summary>
		/// Collects classification runs for an element's tags, attributes, and child nodes.
		/// </summary>
		/// <param name="element">The element to classify.</param>
		/// <param name="range">The range for which runs are requested, in parse snapshot coordinates.</param>
		/// <param name="snapshot">The snapshot the parse result was computed from.</param>
		/// <param name="runs">The list to which runs are added.</param>
		void CollectElementRuns (XElement element, TextSpan range, ITextSnapshot snapshot, List<ClassificationRun> runs)
		{
			if (element.Span.Intersects (range)) {
				if (SnapshotMatches (snapshot, element.Span.Start, "<")) {
					runs.Add (new ClassificationRun (element.Span.Start, 1, typeMap.Delimiter));
				}
				if (element.IsNamed) {
					runs.Add (new ClassificationRun (element.NameSpan, typeMap.ElementName));
				}
				foreach (XAttribute attribute in element.Attributes) {
					CollectAttributeRuns (attribute, snapshot, runs);
				}
				if (element.IsEnded && element.Span.End <= snapshot.Length && snapshot[element.Span.End - 1] == '>') {
					if (element.Span.Length >= 2 && snapshot[element.Span.End - 2] == '/') {
						runs.Add (new ClassificationRun (element.Span.End - 2, 2, typeMap.Delimiter));
					} else {
						runs.Add (new ClassificationRun (element.Span.End - 1, 1, typeMap.Delimiter));
					}
				}
			}

			CollectRuns (element, range, snapshot, runs);

			if (element.ClosingTag is XClosingTag elementClosingTag && elementClosingTag.Span.Intersects (range)) {
				CollectClosingTagRuns (elementClosingTag, snapshot, runs);
			}
		}

		/// <summary>
		/// Collects classification runs for a closing tag's delimiters and name.
		/// </summary>
		/// <param name="closingTag">The closing tag to classify.</param>
		/// <param name="snapshot">The snapshot the parse result was computed from.</param>
		/// <param name="runs">The list to which runs are added.</param>
		void CollectClosingTagRuns (XClosingTag closingTag, ITextSnapshot snapshot, List<ClassificationRun> runs)
		{
			if (SnapshotMatches (snapshot, closingTag.Span.Start, "</")) {
				runs.Add (new ClassificationRun (closingTag.Span.Start, 2, typeMap.Delimiter));
			}
			if (closingTag.IsNamed) {
				runs.Add (new ClassificationRun (closingTag.NameSpan, typeMap.ElementName));
			}
			if (closingTag.IsEnded && closingTag.Span.End <= snapshot.Length && snapshot[closingTag.Span.End - 1] == '>') {
				runs.Add (new ClassificationRun (closingTag.Span.End - 1, 1, typeMap.Delimiter));
			}
		}

		/// <summary>
		/// Collects classification runs for an attribute's name, equals sign, quotes, and value.
		/// </summary>
		/// <param name="attribute">The attribute to classify.</param>
		/// <param name="snapshot">The snapshot the parse result was computed from.</param>
		/// <param name="runs">The list to which runs are added.</param>
		void CollectAttributeRuns (XAttribute attribute, ITextSnapshot snapshot, List<ClassificationRun> runs)
		{
			if (attribute.IsNamed) {
				runs.Add (new ClassificationRun (attribute.NameSpan, typeMap.AttributeName));
			}

			int equalsScanEnd = Math.Min (attribute.HasValue ? attribute.ValueOffset.Value : attribute.Span.End, snapshot.Length);
			for (int position = Math.Max (attribute.NameSpan.End, 0); position < equalsScanEnd; position++) {
				if (snapshot[position] == '=') {
					runs.Add (new ClassificationRun (position, 1, typeMap.Delimiter));
					break;
				}
			}

			if (!attribute.HasValue) {
				return;
			}

			int valueOffset = attribute.ValueOffset.Value;
			int openingQuotePosition = valueOffset - 1;
			char quoteChar = '\0';
			if (openingQuotePosition >= 0 && openingQuotePosition < snapshot.Length && (snapshot[openingQuotePosition] == '"' || snapshot[openingQuotePosition] == '\'')) {
				quoteChar = snapshot[openingQuotePosition];
				runs.Add (new ClassificationRun (openingQuotePosition, 1, typeMap.AttributeQuotes));
			}
			int closingQuotePosition = valueOffset + attribute.Value.Length;
			if (quoteChar != '\0' && closingQuotePosition < snapshot.Length && snapshot[closingQuotePosition] == quoteChar) {
				runs.Add (new ClassificationRun (closingQuotePosition, 1, typeMap.AttributeQuotes));
			}

			if (attribute.Value.Length > 0) {
				AddValueRuns (attribute.Value, valueOffset, typeMap.AttributeValue, runs);
			}
		}

		/// <summary>
		/// Collects classification runs for a processing instruction's delimiters, name, and content.
		/// </summary>
		/// <param name="processingInstruction">The processing instruction to classify.</param>
		/// <param name="snapshot">The snapshot the parse result was computed from.</param>
		/// <param name="runs">The list to which runs are added.</param>
		void CollectProcessingInstructionRuns (XProcessingInstruction processingInstruction, ITextSnapshot snapshot, List<ClassificationRun> runs)
		{
			TextSpan span = processingInstruction.Span;
			if (span.Length == 0 || span.Start < 0 || span.End > snapshot.Length) {
				return;
			}

			int contentStart = span.Start;
			if (SnapshotMatches (snapshot, span.Start, "<?")) {
				runs.Add (new ClassificationRun (span.Start, 2, typeMap.Delimiter));
				contentStart += 2;
			}

			int nameEnd = contentStart;
			while (nameEnd < span.End && XmlChar.IsNameChar (snapshot[nameEnd])) {
				nameEnd++;
			}
			if (nameEnd > contentStart) {
				runs.Add (new ClassificationRun (contentStart, nameEnd - contentStart, typeMap.ElementName));
				contentStart = nameEnd;
			}

			bool hasEndDelimiter = span.Length >= 4 && SnapshotMatches (snapshot, span.End - 2, "?>");
			int contentEnd = hasEndDelimiter ? span.End - 2 : span.End;
			if (contentEnd > contentStart) {
				runs.Add (new ClassificationRun (contentStart, contentEnd - contentStart, typeMap.ProcessingInstruction));
			}
			if (hasEndDelimiter) {
				runs.Add (new ClassificationRun (span.End - 2, 2, typeMap.Delimiter));
			}
		}

		/// <summary>
		/// Collects classification runs for a node with fixed delimiters, e.g. a comment or CDATA section,
		/// classifying the delimiters like Visual Studio's XML editor does.
		/// </summary>
		/// <param name="span">The node's span.</param>
		/// <param name="prefix">The node's opening delimiter, e.g. <c>&lt;!--</c>.</param>
		/// <param name="suffix">The node's closing delimiter, e.g. <c>--&gt;</c>. May be absent for unclosed nodes at end of file.</param>
		/// <param name="contentTag">The tag for the content between the delimiters.</param>
		/// <param name="snapshot">The snapshot the parse result was computed from.</param>
		/// <param name="runs">The list to which runs are added.</param>
		void AddDelimitedRuns (TextSpan span, string prefix, string suffix, ClassificationTag contentTag, ITextSnapshot snapshot, List<ClassificationRun> runs)
		{
			if (span.Length == 0 || span.Start < 0 || span.End > snapshot.Length) {
				return;
			}

			int contentStart = span.Start;
			int contentEnd = span.End;
			if (SnapshotMatches (snapshot, span.Start, prefix)) {
				runs.Add (new ClassificationRun (span.Start, prefix.Length, typeMap.Delimiter));
				contentStart += prefix.Length;
			}
			if (span.Length >= prefix.Length + suffix.Length && SnapshotMatches (snapshot, span.End - suffix.Length, suffix)) {
				contentEnd -= suffix.Length;
				runs.Add (new ClassificationRun (contentEnd, suffix.Length, typeMap.Delimiter));
			}
			if (contentEnd > contentStart) {
				runs.Add (new ClassificationRun (contentStart, contentEnd - contentStart, contentTag));
			}
		}

		/// <summary>
		/// Parses a value as an MSBuild expression and adds classification runs for the expression constructs in it,
		/// filling the segments between them with the given tag and classifying XML entity references.
		/// </summary>
		/// <param name="text">The value text.</param>
		/// <param name="baseOffset">The offset of the value in the parse snapshot.</param>
		/// <param name="fillTag">The tag for non-expression segments, i.e. attribute value or text content.</param>
		/// <param name="runs">The list to which runs are added.</param>
		void AddValueRuns (string text, int baseOffset, ClassificationTag fillTag, List<ClassificationRun> runs)
		{
			if (text.Length == 0) {
				return;
			}

			List<ClassificationRun> expressionRuns = new ();

			try {
				ExpressionNode expression = ExpressionParser.Parse (text, ExpressionOptions.ItemsMetadataAndLists, baseOffset);

				foreach (ExpressionNode node in expression.WithAllDescendants ()) {
					switch (node) {
					case ExpressionProperty property:
						AddExpressionDelimiterRuns (property, text, baseOffset, expressionRuns);
						break;
					case ExpressionItem item:
						AddExpressionDelimiterRuns (item, text, baseOffset, expressionRuns);
						break;
					case ExpressionMetadata metadata:
						AddExpressionDelimiterRuns (metadata, text, baseOffset, expressionRuns);
						if (metadata.IsQualified && metadata.ItemName.Length > 0) {
							expressionRuns.Add (new ClassificationRun (metadata.ItemNameSpan, typeMap.ExpressionName));
						}
						if (!string.IsNullOrEmpty (metadata.MetadataName)) {
							expressionRuns.Add (new ClassificationRun (metadata.MetadataNameSpan, typeMap.ExpressionName));
						}
						break;
					default:
						if (node.Length > 0 && typeMap.GetTagForExpressionNode (node) is ClassificationTag tag) {
							expressionRuns.Add (new ClassificationRun (node.Span, tag));
						}
						break;
					}
				}
			} catch (Exception ex) {
				// the expression parser is not guaranteed to handle partially typed expressions gracefully,
				// so degrade to the plain fill classification rather than losing all tags for the request
				LogExpressionParserError (logger, ex);
				expressionRuns.Clear ();
			}

			expressionRuns.Sort ((a, b) => a.Start.CompareTo (b.Start));

			int position = baseOffset;
			int valueEnd = baseOffset + text.Length;
			foreach (ClassificationRun expressionRun in expressionRuns) {
				if (expressionRun.Start > position) {
					AddGapRuns (text, position - baseOffset, expressionRun.Start - baseOffset, baseOffset, fillTag, runs);
				}
				runs.Add (expressionRun);
				position = Math.Max (position, expressionRun.End);
			}
			if (position < valueEnd) {
				AddGapRuns (text, position - baseOffset, text.Length, baseOffset, fillTag, runs);
			}
		}

		/// <summary>
		/// Adds classification runs for a segment between expression constructs, classifying XML entity
		/// references like <c>&amp;amp;</c> and filling the rest with the given tag.
		/// </summary>
		/// <param name="text">The value text the segment belongs to.</param>
		/// <param name="gapStart">The start of the segment, relative to the value text.</param>
		/// <param name="gapEnd">The end of the segment, relative to the value text.</param>
		/// <param name="baseOffset">The offset of the value in the parse snapshot.</param>
		/// <param name="fillTag">The tag for non-entity parts of the segment.</param>
		/// <param name="runs">The list to which runs are added.</param>
		void AddGapRuns (string text, int gapStart, int gapEnd, int baseOffset, ClassificationTag fillTag, List<ClassificationRun> runs)
		{
			int segmentStart = gapStart;
			int position = gapStart;
			while (position < gapEnd) {
				if (text[position] == '&' && TryMatchEntity (text, position, gapEnd, out int entityLength)) {
					if (position > segmentStart) {
						runs.Add (new ClassificationRun (baseOffset + segmentStart, position - segmentStart, fillTag));
					}
					runs.Add (new ClassificationRun (baseOffset + position, entityLength, typeMap.EntityReference));
					position += entityLength;
					segmentStart = position;
				} else {
					position++;
				}
			}
			if (gapEnd > segmentStart) {
				runs.Add (new ClassificationRun (baseOffset + segmentStart, gapEnd - segmentStart, fillTag));
			}
		}

		/// <summary>
		/// Tries to match an XML entity reference like <c>&amp;amp;</c>, <c>&amp;#10;</c> or <c>&amp;#x0A;</c> at the given position.
		/// </summary>
		/// <param name="text">The text to match in.</param>
		/// <param name="start">The position of the ampersand.</param>
		/// <param name="limit">The exclusive end of the searchable range.</param>
		/// <param name="length">The length of the matched entity reference, including the ampersand and semicolon.</param>
		/// <returns>Whether an entity reference was matched.</returns>
		static bool TryMatchEntity (string text, int start, int limit, out int length)
		{
			length = 0;
			int position = start + 1;
			if (position < limit && text[position] == '#') {
				position++;
				if (position < limit && (text[position] == 'x' || text[position] == 'X')) {
					position++;
				}
			}
			int nameStart = position;
			while (position < limit && position - start <= 32 && char.IsLetterOrDigit (text[position])) {
				position++;
			}
			if (position == nameStart || position >= limit || text[position] != ';') {
				return false;
			}
			length = position - start + 1;
			return true;
		}

		/// <summary>
		/// Adds classification runs for an expression node's delimiters, i.e. the leading <c>$(</c>, <c>@(</c> or <c>%(</c> and the trailing <c>)</c> if present.
		/// </summary>
		/// <param name="node">The property, item or metadata expression node.</param>
		/// <param name="text">The value text the node was parsed from.</param>
		/// <param name="baseOffset">The offset of the value in the parse snapshot.</param>
		/// <param name="runs">The list to which runs are added.</param>
		void AddExpressionDelimiterRuns (ExpressionNode node, string text, int baseOffset, List<ClassificationRun> runs)
		{
			if (node.Length < 2) {
				return;
			}

			runs.Add (new ClassificationRun (node.Offset, 2, typeMap.ExpressionDelimiter));

			int lastCharIndex = node.End - 1 - baseOffset;
			if (node.Length > 2 && lastCharIndex < text.Length && text[lastCharIndex] == ')') {
				runs.Add (new ClassificationRun (node.End - 1, 1, typeMap.ExpressionDelimiter));
			}
		}

		/// <summary>
		/// Checks whether the snapshot contains the expected text at the given position.
		/// </summary>
		/// <param name="snapshot">The snapshot to check.</param>
		/// <param name="position">The position at which the text is expected.</param>
		/// <param name="expectedText">The expected text.</param>
		/// <returns>Whether the snapshot contains the expected text at the position.</returns>
		static bool SnapshotMatches (ITextSnapshot snapshot, int position, string expectedText)
		{
			if (position < 0 || position + expectedText.Length > snapshot.Length) {
				return false;
			}
			for (int i = 0; i < expectedText.Length; i++) {
				if (snapshot[position + i] != expectedText[i]) {
					return false;
				}
			}
			return true;
		}

		readonly struct ClassificationRun
		{
			public ClassificationRun (int start, int length, ClassificationTag tag)
			{
				Start = start;
				Length = length;
				Tag = tag;
			}

			public ClassificationRun (TextSpan span, ClassificationTag tag) : this (span.Start, span.Length, tag)
			{
			}

			public readonly int Start;
			public readonly int Length;
			public readonly ClassificationTag Tag;

			public int End => Start + Length;
		}

		[LoggerMessage (EventId = 0, Level = LogLevel.Debug, Message = "Expression parser failed on partial input, skipping expression classification")]
		static partial void LogExpressionParserError (ILogger logger, Exception ex);
	}
}
