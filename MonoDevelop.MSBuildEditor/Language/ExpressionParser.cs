// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace MonoDevelop.MSBuildEditor.Language
{
	//TODO: property functions, item transforms with custom separators
	static class ExpressionParser
	{
		public static ExpressionNode Parse (string expression, ExpressionOptions options = ExpressionOptions.None, int baseOffset = 0)
		{
			return Parse (expression, 0, expression.Length - 1, options, baseOffset);
		}

		public static ExpressionNode Parse (string buffer, int startOffset, int endOffset, ExpressionOptions options, int baseOffset)
		{
			List<ExpressionNode> splitList = null;
			var nodes = new List<ExpressionNode> ();

			int lastNodeEnd = startOffset;
			for (int offset = startOffset; offset <= endOffset; offset++) {
				char c = buffer [offset];

				if (options.HasFlag (ExpressionOptions.Lists)) {
					if (c == ';' || (c == ',' && options.HasFlag (ExpressionOptions.CommaLists))) {
						CaptureLiteral (offset, nodes.Count == 0);
						if (splitList == null) {
							splitList = new List<ExpressionNode> ();
						}
						FlushNodesToSplitList (offset);
						lastNodeEnd = offset + 1;
						continue;
					}
				}

				int possibleLiteralEndOffset = offset;

				ExpressionNode node;
				switch (c) {
				case '@':
					if (options.HasFlag (ExpressionOptions.Items)) {
						node = ParseItem (buffer, ref offset, endOffset, baseOffset);
					} else {
						node = new ExpressionError (baseOffset + offset, ExpressionErrorKind.ItemsDisallowed);
					}
					break;
				case '$':
					node = ParseProperty (buffer, ref offset, endOffset, baseOffset);
					break;
				case '%':
					if (options.HasFlag (ExpressionOptions.Metadata)) {
						node = ParseMetadata (buffer, ref offset, endOffset, baseOffset);
					} else {
						node = new ExpressionError (baseOffset + offset, ExpressionErrorKind.MetadataDisallowed);
					}
					break;
				default:
					continue;
				}

				CaptureLiteral (possibleLiteralEndOffset, false);
				lastNodeEnd = offset + 1;

				nodes.Add (node);
				if (node is ExpressionError) {
					//short circuit out without capturing the rest as text, since it's not useful
					return CreateResult (offset);
				}
			}

			CaptureLiteral (endOffset + 1, nodes.Count == 0);
			return CreateResult (endOffset);

			void CaptureLiteral (int toOffset, bool isPure)
			{
				if (toOffset > lastNodeEnd) {
					string s = buffer.Substring (lastNodeEnd, toOffset - lastNodeEnd);
					nodes.Add (new ExpressionLiteral (baseOffset + lastNodeEnd, s, isPure));
				}
			}

			void FlushNodesToSplitList (int offset)
			{
				if (nodes.Count == 0) {
					splitList.Add (new ExpressionError (baseOffset + offset, ExpressionErrorKind.EmptyListEntry));
				} else if (nodes.Count == 1) {
					splitList.Add (nodes [0]);
					nodes.Clear ();
				} else {
					var start = nodes [0].Offset;
					var l = nodes [nodes.Count - 1].End - start;
					splitList.Add (new Expression (baseOffset + start, l, nodes.ToArray ()));
					nodes.Clear ();
				}
			}

			ExpressionNode CreateResult (int offset)
			{
				if (splitList != null) {
					FlushNodesToSplitList (offset);
				}
				if (splitList != null) {
					return new ExpressionList (baseOffset + startOffset, endOffset - startOffset + 1, splitList.ToArray ());
				}
				if (nodes.Count == 0) {
					return null;
				}
				if (nodes.Count == 1) {
					return nodes [0];
				}
				return new Expression (baseOffset + startOffset, endOffset - startOffset + 1, nodes.ToArray ());
			}
		}

		static ExpressionNode ParseItem (string buffer, ref int offset, int endOffset, int baseOffset)
		{
			int start = offset;

			offset++;
			if (offset > endOffset || buffer [offset] != '(') {
				return new ExpressionError (baseOffset + offset, ExpressionErrorKind.ExpectingLeftParen);
			}

			offset++;
			string name = ReadName (buffer, ref offset, endOffset);
			if (name == null) {
				return new ExpressionError (baseOffset + offset, ExpressionErrorKind.ExpectingItemName);
			}

			if (offset <= endOffset && buffer [offset] == ')') {
				return new ExpressionItem (baseOffset + start, offset - start + 1, name);
			}

			ConsumeWhitespace (ref offset);

			if (offset > endOffset || buffer [offset] != '-') {
				return new ExpressionError (baseOffset + offset, ExpressionErrorKind.ExpectingRightParenOrDash);
			}

			offset++;
			if (offset > endOffset || buffer [offset] != '>') {
				return new ExpressionError (baseOffset + offset, ExpressionErrorKind.ExpectingRightAngleBracket);
			}

			offset++;
			ConsumeWhitespace (ref offset);

			if (offset > endOffset || buffer [offset] != '\'') {
				return new ExpressionError (baseOffset + offset, ExpressionErrorKind.ExpectingApos);
			}

			offset++;
			var endAposOffset = buffer.IndexOf ('\'', offset, endOffset - offset + 1);
			if (endAposOffset < 0) {
				return new ExpressionError (baseOffset + endOffset, ExpressionErrorKind.ExpectingApos);
			}

			ExpressionNode transform;
			if (endAposOffset == 0) {
				transform = new ExpressionLiteral (offset, "", true);
			} else {
				//FIXME: disallow items in the transform
				transform = Parse (buffer, offset, endAposOffset - 1, ExpressionOptions.Metadata, baseOffset);
			}

			offset = endAposOffset + 1;
			ConsumeWhitespace (ref offset);

			if (offset > endOffset || buffer [offset] != ')') {
				return new ExpressionError (baseOffset + offset, ExpressionErrorKind.ExpectingRightParen);
			}

			return new ExpressionItem (baseOffset + start, offset - start + 1, name, transform);

			void ConsumeWhitespace (ref int o)
			{
				while (o <= endOffset && buffer [o] == ' ') {
					o++;
				}
			}
		}

		static string ReadName (string buffer, ref int offset, int endOffset)
		{
			if (offset > endOffset) {
				return null;
			}

			int start = offset;
			char ch = buffer [offset];
			if (!char.IsLetter (ch) && ch != '_') {
				return null;
			}
			offset++;
			while (offset <= endOffset) {
				ch = buffer [offset];
				if (!char.IsLetterOrDigit (ch) && ch != '_') {
					break;
				}
				offset++;
			}
			return buffer.Substring (start, offset - start);
		}

		static ExpressionNode ParseProperty (string buffer, ref int offset, int endOffset, int baseOffset)
		{
			int start = offset;

			offset++;
			if (offset > endOffset || buffer [offset] != '(') {
				return new ExpressionError (baseOffset + offset, ExpressionErrorKind.ExpectingLeftParen);
			}

			offset++;
			string name = ReadName (buffer, ref offset, endOffset);
			if (name == null) {
				return new ExpressionError (baseOffset + offset, ExpressionErrorKind.ExpectingPropertyName);
			}

			if (offset > endOffset || buffer [offset] != ')') {
				return new ExpressionError (baseOffset + offset, ExpressionErrorKind.ExpectingRightParen);
			}

			return new ExpressionProperty (baseOffset + start, offset - start + 1, name);
		}

		static ExpressionNode ParseMetadata (string buffer, ref int offset, int endOffset, int baseOffset)
		{
			int start = offset;

			offset++;
			if (offset > endOffset || buffer [offset] != '(') {
				return new ExpressionError (baseOffset + offset, ExpressionErrorKind.ExpectingLeftParen);
			}

			offset++;
			string name = ReadName (buffer, ref offset, endOffset);
			if (name == null) {
				return new ExpressionError (baseOffset + offset, ExpressionErrorKind.ExpectingMetadataOrItemName);
			}

			if (offset <= endOffset && buffer [offset] == ')') {
				return new ExpressionMetadata (baseOffset + start, offset - start, null, name);
			}

			if (offset > endOffset || buffer [offset] != '.') {
				return new ExpressionError (baseOffset + offset, ExpressionErrorKind.ExpectingRightParenOrPeriod);
			}

			offset++;
			string metadataName = ReadName (buffer, ref offset, endOffset);
			if (metadataName == null) {
				return new ExpressionError (baseOffset + offset, ExpressionErrorKind.ExpectingMetadataName);
			}

			if (offset > endOffset || buffer [offset] != ')') {
				return new ExpressionError (baseOffset + offset, ExpressionErrorKind.ExpectingRightParen);
			}

			return new ExpressionMetadata (baseOffset + start, offset - start + 1, name, metadataName);
		}
	}
}
