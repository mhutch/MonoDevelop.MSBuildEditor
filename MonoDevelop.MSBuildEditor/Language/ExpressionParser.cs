// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace MonoDevelop.MSBuildEditor.Language
{
	//TODO: property functions, item transforms with custom separators, item functions
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

				//consume entities simply so the semicolon doesn't mess with list parsing
				//we don't need the value and the base XML editor will handle errors
				if (c == '&') {
					offset++;
					//FIXME: use proper entity name logic. this will do for now.
					var name = ReadName (buffer, ref offset, endOffset);
					if (offset > endOffset) {
						break;
					}
					if (buffer[offset] == ';') {
						continue;
					}
					c = buffer [offset];
				}

				if ((options.HasFlag (ExpressionOptions.Lists) && c == ';') || (c == ',' && options.HasFlag (ExpressionOptions.CommaLists))) {
					CaptureLiteral (offset, nodes.Count == 0);
					if (splitList == null) {
						splitList = new List<ExpressionNode> ();
					}
					FlushNodesToSplitList (offset);
					lastNodeEnd = offset + 1;
					continue;
				}

				int possibleLiteralEndOffset = offset;

				ExpressionNode node;
				switch (c) {
				case '@':
					if (!TryConsumeParen ()) {
						continue;
					}
					if (options.HasFlag (ExpressionOptions.Items)) {
						node = ParseItem (buffer, ref offset, endOffset, baseOffset);
					} else {
						node = new ExpressionError (baseOffset + offset, ExpressionErrorKind.ItemsDisallowed);
					}
					break;
				case '$':
					if (!TryConsumeParen ()) {
						continue;
					}
					node = ParseProperty (buffer, ref offset, endOffset, baseOffset);
					break;
				case '%':
					if (!TryConsumeParen ()) {
						continue;
					}
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

				bool TryConsumeParen ()
				{
					if (offset < endOffset && buffer[offset+1] == '(') {
						offset++;
						offset++;
						return true;
					}
					return false;
				}
			}

			CaptureLiteral (endOffset + 1, nodes.Count == 0);
			return CreateResult (endOffset);

			void CaptureLiteral (int toOffset, bool isPure)
			{
				if (toOffset > lastNodeEnd) {
					string s = buffer.Substring (lastNodeEnd, toOffset - lastNodeEnd);
					nodes.Add (new ExpressionText (baseOffset + lastNodeEnd, s, isPure));
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
					splitList.Add (new Expression (start, l, nodes.ToArray ()));
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
					return new ExpressionText (baseOffset + startOffset, "", true);
				}
				if (nodes.Count == 1) {
					return nodes [0];
				}
				return new Expression (baseOffset + startOffset, endOffset - startOffset + 1, nodes.ToArray ());
			}
		}

		static ExpressionNode ParseItem (string buffer, ref int offset, int endOffset, int baseOffset)
		{
			int start = offset - 2;

			string name = ReadName (buffer, ref offset, endOffset);
			if (name == null) {
				return new ExpressionError (baseOffset + offset, ExpressionErrorKind.ExpectingItemName);
			}

			if (offset <= endOffset && buffer [offset] == ')') {
				return new ExpressionItem (baseOffset + start, offset - start + 1, name);
			}

			ConsumeWhitespace (ref offset);

			if (offset > endOffset || buffer [offset] != '-') {
				return new IncompleteExpressionError (
					baseOffset + offset, offset > endOffset, ExpressionErrorKind.ExpectingRightParenOrDash,
					new ExpressionItem (baseOffset + start, offset - start + 1, name)
				);
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
				transform = new ExpressionText (offset, "", true);
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

		static void ConsumeSpace (string buffer, ref int offset, int endOffset)
		{
			while (offset <= endOffset) {
				var ch = buffer [offset];
				if (ch != ' ') {
					return;
				}
				offset++;
			}
		}

		//attempts to read int or double token. assumes offset is already at first character
		static ExpressionNode ReadArgumentNumber (string buffer, ref int offset, int endOffset, int baseOffset)
		{
			int start = offset;
			bool foundPeriod = false;
			while (offset <= endOffset) {
				char ch = buffer [offset];
				switch (ch) {
				case '.':
					if (foundPeriod) {
						break;
					}
					foundPeriod = true;
					goto case '0';
				case ',': case ')': case ' ': {
						string str = buffer.Substring (start + baseOffset, offset - start);
						if (foundPeriod) {
							if (double.TryParse (str, out double result)) {
								return new ExpressionArgumentFloat (start + baseOffset, str.Length, result);
							}
						} else {
							if (long.TryParse (str, out long result)) {
								return new ExpressionArgumentInt (start + baseOffset, str.Length, result);
							}
						}
						return new ExpressionError (start, ExpressionErrorKind.CouldNotParseNumber);
					}
				case '0': case '1': case '2': case '3': case '4': case '5': case '6': case '7': case '8': case '9':
					offset++;
					continue;
				}
			}
			if (foundPeriod && (offset - start) == 1) {
				return new ExpressionError (start + baseOffset, ExpressionErrorKind.IncompleteValue);
			}
			return new ExpressionError (offset + baseOffset, ExpressionErrorKind.ExpectingRightParenOrComma);
		}

		static ExpressionNode ParseProperty (string buffer, ref int offset, int endOffset, int baseOffset)
		{
			int start = offset - 2;

			ConsumeSpace (buffer, ref offset, endOffset);

			if (offset <= endOffset && buffer [offset] == '[') {
				return ParsePropertyStaticFunction (start, buffer, ref offset, endOffset, baseOffset);
			}

			string name = ReadName (buffer, ref offset, endOffset);
			if (name == null) {
				return new ExpressionError (baseOffset + offset, ExpressionErrorKind.ExpectingPropertyName);
			}

			ExpressionPropertyNode propRef = new ExpressionPropertyName (baseOffset + offset - name.Length, name.Length, name);

			ConsumeSpace (buffer, ref offset, endOffset);

			bool foundFunction = false;
			if (offset <= endOffset && buffer [offset] == '.') {
				if (WrapError (
					ParsePropertyStringFunction (buffer, ref offset, endOffset, baseOffset, propRef),
					out propRef,
					out IncompleteExpressionError error,
					(n,o) => new ExpressionProperty (baseOffset + start, o - baseOffset - start, n)
				)) {
					return error;
				}
				foundFunction = true;
			}

			ConsumeSpace (buffer, ref offset, endOffset);

			if (offset > endOffset || buffer [offset] != ')') {
				return new IncompleteExpressionError (
					baseOffset + offset, offset > endOffset,
					foundFunction? ExpressionErrorKind.ExpectingRightParen : ExpressionErrorKind.ExpectingRightParenOrPeriod,
					new ExpressionProperty (baseOffset + start, offset - start, propRef)
				);
			}

			return new ExpressionProperty (baseOffset + start, offset - start + 1, propRef);
		}

		static ExpressionNode ParsePropertyStaticFunction(int start, string buffer, ref int offset, int endOffset, int baseOffset)
		{
			return new ExpressionError (baseOffset + offset, ExpressionErrorKind.PropertyFunctionsNotSupported);
		}

		static bool WrapError<T> (ExpressionNode result, out T success, out IncompleteExpressionError error, Func<T,int,ExpressionNode> wrap) where T : ExpressionNode
		{
			success = null;
			error = null;
			if (result is ExpressionError ee) {
				var iee = ee as IncompleteExpressionError;
				error = new IncompleteExpressionError (
					ee.Offset,
					iee?.WasEOF ?? false,
					ee.Kind,
					wrap (iee?.IncompleteNode as T, ee.Offset)
				);
				return true;
			}
			success = (T)result;
			return false;
		}

		static ExpressionNode ParsePropertyStringFunction(string buffer, ref int offset, int endOffset, int baseOffset, ExpressionPropertyNode target)
		{
			offset++;

			ConsumeSpace (buffer, ref offset, endOffset);

			var methodName = ReadName (buffer, ref offset, endOffset);
			if (methodName == null) {
				return new IncompleteExpressionError (baseOffset + offset, offset > endOffset, ExpressionErrorKind.ExpectingMethodName, target);
			}

			ConsumeSpace (buffer, ref offset, endOffset);

			if (offset > endOffset || buffer [offset] != '(') {
				return new IncompleteExpressionError (
					baseOffset + offset,
					offset > endOffset,
					ExpressionErrorKind.ExpectingLeftParen,
					new ExpressionPropertyFunctionInvocation (target.Offset, (offset + baseOffset) - target.Offset, target, methodName, null)
				);
			}

			if (WrapError (
				ParseFunctionArgumentList  (buffer, ref offset, endOffset, baseOffset),
				out ExpressionArgumentList args,
				out IncompleteExpressionError error,
				(n,o) => new ExpressionPropertyFunctionInvocation (target.Offset, o - target.Offset, target, methodName, n)
			)) {
				return error;
			}

			return new ExpressionPropertyFunctionInvocation (target.Offset, (offset + baseOffset) - target.Offset, target, methodName, args);
		}

		static ExpressionNode ParseFunctionArgumentList (string buffer, ref int offset, int endOffset, int baseOffset)
		{
			int start = offset - 1;

			offset++;
			ConsumeSpace (buffer, ref offset, endOffset);

			var values = new List<ExpressionNode> ();
			bool first = true;

			while (true) {
				if (offset <= endOffset && buffer [offset] == ')') {
					offset++;
					break;
				}

				bool foundComma = false;
				if (!first && offset <= endOffset && buffer [offset] == ',') {
					foundComma = true;
					offset++;
					ConsumeSpace (buffer, ref offset, endOffset);
				}

				if (offset > endOffset || (!first && !foundComma)) {
					return new IncompleteExpressionError (
						baseOffset + offset,
						offset > endOffset,
						first ? ExpressionErrorKind.ExpectingRightParenOrValue
							: (foundComma? ExpressionErrorKind.ExpectingValue
								: ExpressionErrorKind.ExpectingRightParenOrComma),
						new ExpressionArgumentList (baseOffset + start, offset - start, values));
				}

				if (WrapError (
					ParseFunctionArgument (first, buffer, ref offset, endOffset, baseOffset),
					out ExpressionNode arg,
					out IncompleteExpressionError err,
					(n,o) => {
						if (n != null) {
							values.Add (n);
						}
						return new ExpressionArgumentList (baseOffset + start, o - baseOffset - start, values);
						})
				   ) {
					return err;
				}

				values.Add (arg);
				ConsumeSpace (buffer, ref offset, endOffset);

				first = false;
			}

			return new ExpressionArgumentList (baseOffset + start, offset - start, values);
		}

		//expects to be advanced to valid first char of argument
		static ExpressionNode ParseFunctionArgument (bool wasFirst, string buffer, ref int offset, int endOffset, int baseOffset)
		{
			int start = offset;

			var ch = buffer [start];
			if (ch == '.' || char.IsDigit (ch)) {
				return ReadArgumentNumber (buffer, ref offset, endOffset, baseOffset);
			}

			var name = ReadName (buffer, ref offset, endOffset);
			if (bool.TryParse (name, out bool val)) {
				return new ExpressionArgumentBool (start + baseOffset, name.Length, val);
			}
			offset = start;

			return new ExpressionError (offset, wasFirst? ExpressionErrorKind.ExpectingRightParenOrValue : ExpressionErrorKind.ExpectingValue);
		}

		static ExpressionNode ParseMetadata (string buffer, ref int offset, int endOffset, int baseOffset)
		{
			int start = offset - 2;

			string name = ReadName (buffer, ref offset, endOffset);
			if (name == null) {
				return new ExpressionError (baseOffset + offset, ExpressionErrorKind.ExpectingMetadataOrItemName);
			}

			if (offset <= endOffset && buffer [offset] == ')') {
				return new ExpressionMetadata (baseOffset + start, offset - start, null, name);
			}

			if (offset > endOffset || buffer [offset] != '.') {
				return new IncompleteExpressionError (
					baseOffset + offset, offset > endOffset, ExpressionErrorKind.ExpectingRightParenOrPeriod,
					new ExpressionMetadata (baseOffset + start, offset - start + 1, name, null)
				);
			}

			offset++;
			string metadataName = ReadName (buffer, ref offset, endOffset);
			if (metadataName == null) {
				return new IncompleteExpressionError (
					baseOffset + offset, offset > endOffset, ExpressionErrorKind.ExpectingMetadataName,
					new ExpressionMetadata (baseOffset + start, offset - start + 1, name, null)
				);
			}

			if (offset > endOffset || buffer [offset] != ')') {
				return new IncompleteExpressionError (
					baseOffset + offset, offset > endOffset, ExpressionErrorKind.ExpectingRightParen,
					new ExpressionMetadata (baseOffset + start, offset - start + 1, name, metadataName)
				);
			}

			return new ExpressionMetadata (baseOffset + start, offset - start + 1, name, metadataName);
		}
	}
}
