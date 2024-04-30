// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Language.Syntax;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Editor;
using MonoDevelop.Xml.Editor.Logging;
using MonoDevelop.Xml.Logging;
using MonoDevelop.Xml.Parser;

namespace MonoDevelop.MSBuild.Editor.TextStructure
{
	class MSBuildTextStructureNavigator : ITextStructureNavigator
	{
		readonly MSBuildTextStructureNavigatorProvider provider;
		readonly ITextBuffer textBuffer;
		readonly ITextStructureNavigator xmlNavigator;

		public MSBuildTextStructureNavigator (ITextBuffer textBuffer, MSBuildTextStructureNavigatorProvider provider)
		{
			this.textBuffer = textBuffer;
			this.provider = provider;
			xmlNavigator = provider.NavigatorService.CreateTextStructureNavigator (
				textBuffer,
				provider.ContentTypeRegistry.GetContentType (XmlContentTypeNames.XmlCore)
			);
		}

		public IContentType ContentType => textBuffer.ContentType;

		public TextExtent GetExtentOfWord (SnapshotPoint currentPosition) => xmlNavigator.GetExtentOfWord (currentPosition);

		public SnapshotSpan GetSpanOfFirstChild (SnapshotSpan activeSpan) => xmlNavigator.GetSpanOfFirstChild (activeSpan);

		public SnapshotSpan GetSpanOfNextSibling (SnapshotSpan activeSpan) => xmlNavigator.GetSpanOfNextSibling (activeSpan);

		public SnapshotSpan GetSpanOfPreviousSibling (SnapshotSpan activeSpan) => xmlNavigator.GetSpanOfPreviousSibling (activeSpan);

		enum SelectionLevel
		{
			Self,
			Name,
			Content,
			OuterElement,
			Document,
			Attributes
		}

		public SnapshotSpan GetSpanOfEnclosing (SnapshotSpan activeSpan)
			=> provider.LoggerFactory.GetLogger<MSBuildTextStructureNavigator> (textBuffer).InvokeAndLogExceptions (() => GetSpanOfEnclosingInternal (activeSpan));

		SnapshotSpan GetSpanOfEnclosingInternal (SnapshotSpan activeSpan)
		{
			if (!provider.XmlParserProvider.TryGetParser (activeSpan.Snapshot.TextBuffer, out var parser)) {
				return xmlNavigator.GetSpanOfEnclosing (activeSpan);
			}

			// use last parse if it's up to date, which is most likely will be
			// else use a spine from the end of the selection and update as needed
			var lastParse = parser.LastOutput;
			List<XObject> nodePath;
			XmlSpineParser spine = null;
			if (lastParse != null && lastParse.TextSnapshot.Version.VersionNumber == activeSpan.Snapshot.Version.VersionNumber) {
				var n = lastParse.XDocument.FindAtOrBeforeOffset (activeSpan.Start.Position);
				nodePath = n.GetPath ();
			} else {
				spine = parser.GetSpineParser (activeSpan.Start);
				if (!spine.TryAdvanceToNodeEndAndGetNodePath (activeSpan.Snapshot, out nodePath)) {
					return xmlNavigator.GetSpanOfEnclosing (activeSpan);
				}
			}

			if (nodePath.Count > 0) {
				var leaf = nodePath[nodePath.Count - 1];
				if (TryGetValueSpan (leaf) is SnapshotSpan valueSpan) {
					return valueSpan;
				}
			}

			return xmlNavigator.GetSpanOfEnclosing (activeSpan);


			SnapshotSpan? TryGetValueSpan (XObject leaf)
			{
				if (leaf is not XAttribute && leaf is not XText) {
					return null;
				}

				var syntax = MSBuildElementSyntax.Get (nodePath);
				if (syntax is null) {
					return null;
				}

				int offset = 0;
				string text = null;
				bool isCondition = false;

				if (leaf is XText t) {
					offset = t.Span.Start;
					text = t.Text;
				} else if (leaf is XAttribute att && att.TryGetValue (out var attVal)) {
					offset = att.ValueOffset.Value;
					text = att.Value;
					isCondition = true;
				} else {
					return null;
				}

				var expr = isCondition
					? ExpressionParser.ParseCondition (text, offset)
					: ExpressionParser.Parse (text, ExpressionOptions.ItemsMetadataAndLists, offset);

				var expansion = Expand (activeSpan, expr, out var isText);

				if (expansion is SnapshotSpan expandedSpan) {
					if (isText) {
						var xmlNavigatorSpan = xmlNavigator.GetSpanOfEnclosing (activeSpan);
						if (expandedSpan.Contains (xmlNavigatorSpan)) {
							return xmlNavigatorSpan;
						}
					}
					return expandedSpan;
				}

				return null;
			}
		}

		SnapshotSpan? Expand (SnapshotSpan activeSpan, ExpressionNode expr, out bool isText)
		{
			isText = false;

			var startNode = expr.Find (activeSpan.Start.Position);
			if (startNode == null) {
				return null;
			}

			var endNode = startNode.End == activeSpan.End.Position? startNode : expr.Find (activeSpan.End.Position);
			if (endNode == null) {
				return null;
			}

			var commonAncestor = startNode.FindCommonAncestor (endNode);
			if (commonAncestor == null) {
				return null;
			}

			if (commonAncestor.Span.Start < activeSpan.Start.Position || commonAncestor.Span.Length > activeSpan.Length) {
				return GetSpan (commonAncestor, out isText);
			}

			commonAncestor = commonAncestor.Parent;
			if (commonAncestor == null) {
				return null;
			}

			if (commonAncestor.Span.Start < activeSpan.Start.Position || commonAncestor.Span.Length > activeSpan.Length) {
				return GetSpan (commonAncestor, out isText);
			}

			return null;

			SnapshotSpan GetSpan (ExpressionNode n, out bool isText)
			{
				isText = n.NodeKind switch {
					ExpressionNodeKind.ArgumentLiteralString => true,
					ExpressionNodeKind.Text => true,
					_ => false
				};
				return new SnapshotSpan (activeSpan.Snapshot, commonAncestor.Span.Start, commonAncestor.Span.Length);
			}
		}
	}
}
