// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace MonoDevelop.MSBuild.Language.Expressions
{
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
					splitList.Add (new ConcatExpression (start, l, nodes.ToArray ()));
					nodes.Clear ();
				}
			}

			ExpressionNode CreateResult (int offset)
			{
				if (splitList != null) {
					FlushNodesToSplitList (offset);
				}
				if (splitList != null) {
					return new ListExpression (baseOffset + startOffset, endOffset - startOffset + 1, splitList.ToArray ());
				}
				if (nodes.Count == 0) {
					return new ExpressionText (baseOffset + startOffset, "", true);
				}
				if (nodes.Count == 1) {
					return nodes [0];
				}
				return new ConcatExpression (baseOffset + startOffset, endOffset - startOffset + 1, nodes.ToArray ());
			}
		}

		static ExpressionNode ParseItem (string buffer, ref int offset, int endOffset, int baseOffset)
		{
			int start = offset - 2;

			ConsumeWhitespace (ref offset);

			string name = ReadName (buffer, ref offset, endOffset);
			if (name == null) {
				return new ExpressionError (baseOffset + offset, ExpressionErrorKind.ExpectingItemName);
			}

			ExpressionItemNode itemRef = new ExpressionItemName (baseOffset + offset - name.Length, name.Length, name);

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
				return new IncompleteExpressionError (
					baseOffset + offset, offset > endOffset, ExpressionErrorKind.ExpectingRightAngleBracket,
					new ExpressionItem (baseOffset + start, offset - start + 1, name)
				);
			}

			offset++;
			ConsumeWhitespace (ref offset);

			if (offset > endOffset || !(buffer [offset] == '\'' || char.IsLetter (buffer [offset]))) {
				return new IncompleteExpressionError (
					baseOffset + offset, offset > endOffset, ExpressionErrorKind.ExpectingMethodOrTransform,
					new ExpressionItem (baseOffset + start, offset - start + 1, name)
				);
			}

			char ch = buffer [offset];
			if (ch == '\'' || ch == '`' || ch == '"') {
				if (WrapError (
					ParseItemTransform (buffer, ref offset, endOffset, baseOffset, itemRef),
					out itemRef,
					out IncompleteExpressionError error,
					(n, o) => new ExpressionItem (baseOffset + start, o - start + 1, n)
				)) {
					return error;
				}
			} else {
				if (WrapError (
					ParseItemFunction (buffer, ref offset, endOffset, baseOffset, itemRef),
					out itemRef,
					out IncompleteExpressionError error,
					(n, o) => new ExpressionItem (baseOffset + start, o - start + 1, n)
				)) {
					return error;
				}
			}

			ConsumeWhitespace (ref offset);

			if (offset > endOffset || buffer [offset] != ')') {
				return new IncompleteExpressionError (
					baseOffset + offset, offset > endOffset,
					ExpressionErrorKind.ExpectingRightParen,
					new ExpressionItem (baseOffset + start, offset - start, itemRef)
				);
			}

			return new ExpressionItem (baseOffset + start, offset - start + 1, itemRef);


			void ConsumeWhitespace (ref int o)
			{
				while (o <= endOffset && buffer [o] == ' ') {
					o++;
				}
			}
		}

		static ExpressionNode ParseItemTransform (string buffer, ref int offset, int endOffset, int baseOffset, ExpressionItemNode target)
		{
			char terminator = buffer [offset];

			if (WrapError (
				ReadArgumentString (terminator, buffer, ref offset, endOffset, baseOffset),
				out ExpressionNode expr,
				out IncompleteExpressionError err,
				(n, o) => new ExpressionItemTransform (target.Offset, o - target.Offset + baseOffset, target, n, null)
				)) {
				return err;
			}

			int preSpaceOffset = offset;
			ConsumeSpace (buffer, ref offset, endOffset);

			if (offset > endOffset || buffer [offset] != ',') {
				return new ExpressionItemTransform (target.Offset, preSpaceOffset - target.Offset + baseOffset, target, expr, null);
			}
			offset++;

			ConsumeSpace (buffer, ref offset, endOffset);

			bool foundCustomSeparator = false;
			if (offset <= endOffset) {
				terminator = buffer [offset];
				if (terminator == '\'' || terminator == '"' || terminator == '`') {
					foundCustomSeparator = true;
				}
			}

			if (!foundCustomSeparator) {
				return new IncompleteExpressionError (
					offset,
					offset > endOffset,
					ExpressionErrorKind.ExpectingValue,
					new ExpressionItemTransform (target.Offset, preSpaceOffset - target.Offset + baseOffset, target, expr, null)
				);
			}

			if (WrapError (
				ReadArgumentString (terminator, buffer, ref offset, endOffset, baseOffset),
				out ExpressionNode sepExpr,
				out err,
				(n, o) => new ExpressionItemTransform (target.Offset, o - target.Offset + baseOffset, target, expr, n)
				)) {
				return err;
			}

			return new ExpressionItemTransform (target.Offset, offset - target.Offset + baseOffset, target, expr, sepExpr);
		}

		static ExpressionNode ParseItemFunction (string buffer, ref int offset, int endOffset, int baseOffset, ExpressionItemNode target)
		{
			int nameOffset = offset;
			var funcNameStr = ReadName (buffer, ref offset, endOffset);
			if (funcNameStr == null) {
				return new IncompleteExpressionError (
					baseOffset + offset,
					offset > endOffset,
					ExpressionErrorKind.ExpectingMethodName,
					new ExpressionItemFunctionInvocation (target.Offset, (offset + baseOffset) - target.Offset, target, null, null));
			}
			var funcName = new ExpressionFunctionName (nameOffset + baseOffset, funcNameStr);

			ConsumeSpace (buffer, ref offset, endOffset);

			if (offset > endOffset || buffer [offset] != '(') {
				return new IncompleteExpressionError (
					baseOffset + offset,
					offset > endOffset,
					ExpressionErrorKind.ExpectingLeftParen,
					new ExpressionItemFunctionInvocation (target.Offset, (offset + baseOffset) - target.Offset, target, funcName, null)
				);
			}

			if (WrapError (
				ParseFunctionArgumentList (buffer, ref offset, endOffset, baseOffset),
				out ExpressionArgumentList args,
				out IncompleteExpressionError error,
				(n, o) => new ExpressionItemFunctionInvocation (target.Offset, o - target.Offset, target, funcName, n)
			)) {
				return error;
			}

			return new ExpressionItemFunctionInvocation (target.Offset, (offset + baseOffset) - target.Offset, target, funcName, args);
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

		enum ClassRefParseState
		{
			Initial,
			Component,
			ExpectingPeriod,
			ExpectingComponent
		}

		static ExpressionNode ReadClassReference (string buffer, ref int offset, int endOffset, int baseOffset)
		{
			int start = offset;
			int lastCharOffset = start;

			var sb = new StringBuilder ();
			ClassRefParseState state = ClassRefParseState.Initial;

			while (offset <= endOffset) {
				char ch = buffer [offset];

				switch (state) {
				case ClassRefParseState.Initial:
					if (char.IsLetter (ch) || ch == '_') {
						sb.Append (ch);
						lastCharOffset = offset;
						state = ClassRefParseState.Component;
						break;
					}
					return new ExpressionError (baseOffset + offset, ExpressionErrorKind.ExpectingClassName);

				case ClassRefParseState.Component:
					if (char.IsLetterOrDigit (ch) || ch == '_') {
						sb.Append (ch);
						lastCharOffset = offset;
						break;
					}
					if (ch == '.') {
						sb.Append (ch);
						lastCharOffset = offset;
						state = ClassRefParseState.ExpectingComponent;
						break;
					}
					if (char.IsWhiteSpace (ch)) {
						state = ClassRefParseState.ExpectingPeriod;
						break;
					}
					return new ExpressionClassReference (baseOffset + start, lastCharOffset - start + 1, sb.ToString ());

				case ClassRefParseState.ExpectingComponent:
					if (char.IsLetter (ch) || ch == '_') {
						sb.Append (ch);
						lastCharOffset = offset;
						state = ClassRefParseState.Component;
						break;
					}
					if (char.IsWhiteSpace (ch)) {
						break;
					}
					return new ExpressionError (baseOffset + offset, ExpressionErrorKind.ExpectingClassNameComponent);

				case ClassRefParseState.ExpectingPeriod:
					if (char.IsWhiteSpace (ch)) {
						break;
					}
					if (ch == '.') {
						sb.Append (ch);
						lastCharOffset = offset;
						state = ClassRefParseState.ExpectingComponent;
						break;
					}
					return new ExpressionClassReference (baseOffset + start, lastCharOffset - start + 1, sb.ToString ());
				}
				offset++;
			}

			switch (state) {
			case ClassRefParseState.Initial:
				return new ExpressionError (offset, true, ExpressionErrorKind.ExpectingClassName);
			case ClassRefParseState.ExpectingComponent:
				return new IncompleteExpressionError (offset, true, ExpressionErrorKind.ExpectingClassNameComponent,
					new ExpressionClassReference (baseOffset + start, lastCharOffset - start + 1, sb.ToString ())
				);
			}

			return new ExpressionClassReference (baseOffset + start, lastCharOffset - start + 1, sb.ToString ());
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
				case ',': case ')': case ' ': case ']': {
						string str = buffer.Substring (start, offset - start);
						if (foundPeriod) {
							if (double.TryParse (str, out double result)) {
								return new ExpressionArgumentFloat (start + baseOffset, str.Length, result);
							}
						} else {
							if (long.TryParse (str, out long result)) {
								return new ExpressionArgumentInt (start + baseOffset, str.Length, result);
							}
						}
						return new ExpressionError (start + baseOffset, ExpressionErrorKind.CouldNotParseNumber);
					}
				case '0': case '1': case '2': case '3': case '4': case '5': case '6': case '7': case '8': case '9':
					offset++;
					continue;
				default:
					return new ExpressionError (start + baseOffset, ExpressionErrorKind.CouldNotParseNumber);
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

			ExpressionPropertyNode propRef;

			if (offset <= endOffset && buffer [offset] == '[') {
				if (WrapError (
					ParsePropertyStaticFunction (offset, buffer, ref offset, endOffset, baseOffset),
					out propRef,
					out IncompleteExpressionError error,
					(n, o) => new ExpressionProperty (baseOffset + start, o - baseOffset - start, n)
					)) {
					return error;
				}
			} else {
				string name = ReadName (buffer, ref offset, endOffset);
				if (name == null) {
					return new ExpressionError (baseOffset + offset, ExpressionErrorKind.ExpectingPropertyName);
				}

				propRef = new ExpressionPropertyName (baseOffset + offset - name.Length, name.Length, name);
			}

			ConsumeSpace (buffer, ref offset, endOffset);

			if (offset <= endOffset
				&& buffer [offset] == ':'
				&& propRef is ExpressionPropertyName regStr
				&& string.Equals ("registry", regStr.Name, StringComparison.OrdinalIgnoreCase)
				) {
				offset++;
				ConsumeSpace (buffer, ref offset, endOffset);
				int regStart = offset;
				while (offset <= endOffset) {
					char ch = buffer [offset];

					switch (ch) {
					case '\\':
					case '@':
					case '_':
					case '.':
						offset++;
						continue;
					case ' ':
						ConsumeSpace (buffer, ref offset, endOffset);
						goto case ')';
					case ')':
						string value = buffer.Substring (regStart, offset - regStart);
						propRef = new ExpressionPropertyRegistryValue (propRef.Offset, offset - propRef.Offset + baseOffset, value);
						break;
					default:
						if (char.IsLetterOrDigit (ch)) {
							goto case '.';
						}
						string v = buffer.Substring (regStart, offset - regStart);
						propRef = new ExpressionPropertyRegistryValue (propRef.Offset, offset - propRef.Offset + baseOffset, v);
						// as the current char is not ')', this will turn into an error
						break;
					}
					break;
				}
			} else {
				char c;
				while (offset <= endOffset && ((c=buffer [offset]) == '.' || c == '[')) {
					if (WrapError (
						c == '.'
							? ParsePropertyInstanceFunction (buffer, ref offset, endOffset, baseOffset, propRef)
							: ParsePropertyIndexer (buffer, ref offset, endOffset, baseOffset, propRef)
						,
						out propRef,
						out IncompleteExpressionError error,
						(n, o) => new ExpressionProperty (baseOffset + start, o - baseOffset - start, n)
					)) {
						return error;
					}
					ConsumeSpace (buffer, ref offset, endOffset);

				}
			}

			if (offset > endOffset || buffer [offset] != ')') {
				return new IncompleteExpressionError (
					baseOffset + offset, offset > endOffset,
					ExpressionErrorKind.ExpectingRightParenOrPeriod,
					new ExpressionProperty (baseOffset + start, offset - start, propRef)
				);
			}

			return new ExpressionProperty (baseOffset + start, offset - start + 1, propRef);
		}

		static ExpressionNode ParsePropertyStaticFunction (int start, string buffer, ref int offset, int endOffset, int baseOffset)
		{
			offset++;
			ConsumeSpace (buffer, ref offset, endOffset);

			var className = ReadClassReference (buffer, ref offset, endOffset, baseOffset);
			if (WrapError (
				className,
				out ExpressionClassReference classRef,
				out IncompleteExpressionError err,
				(n, o) => new ExpressionPropertyFunctionInvocation (baseOffset + start, o - start, n, null, null)
			)) {
				return err;
			}

			if (offset + 2 > endOffset || buffer [offset] != ']' || buffer [offset+1] != ':' || buffer [offset+2] != ':') {
				return new IncompleteExpressionError (
					baseOffset + offset,
					offset + 2 > endOffset,
					ExpressionErrorKind.ExpectingBracketColonColon,
					new ExpressionPropertyFunctionInvocation (baseOffset + start, offset - start, classRef, null, null)
				);
			}
			offset += 3;

			ConsumeSpace (buffer, ref offset, endOffset);

			int nameOffset = offset;
			var funcNameStr = ReadName (buffer, ref offset, endOffset);
			if (funcNameStr == null) {
				return new IncompleteExpressionError (
					baseOffset + offset,
					offset > endOffset,
					ExpressionErrorKind.ExpectingMethodName,
					new ExpressionPropertyFunctionInvocation (baseOffset + start, offset - start, classRef, null, null));
			}
			var funcName = new ExpressionFunctionName (baseOffset + nameOffset, funcNameStr);

			ConsumeSpace (buffer, ref offset, endOffset);

			if (offset > endOffset) {
				return new IncompleteExpressionError (
					baseOffset + offset,
					offset > endOffset,
					ExpressionErrorKind.IncompleteProperty,
					new ExpressionPropertyFunctionInvocation (baseOffset + start, offset - start, classRef, funcName, null)
				);
			}

			//arguments are optional, it could be a property
			ExpressionArgumentList args = null;
			if (buffer[offset] == '(') {
				if (WrapError (
					ParseFunctionArgumentList (buffer, ref offset, endOffset, baseOffset),
					out args,
					out IncompleteExpressionError error,
					(n, o) => new ExpressionPropertyFunctionInvocation (baseOffset + start, o - start, classRef, funcName, n)
				)) {
					return error;
				}
			}

			return new ExpressionPropertyFunctionInvocation (baseOffset + start, offset - start, classRef, funcName, args);
		}

		static bool WrapError<T> (ExpressionNode result, out T success, out IncompleteExpressionError error, Func<T,int,ExpressionNode> wrap) where T : ExpressionNode
		{
			success = null;
			error = null;
			if (result is ExpressionError ee) {
				var iee = ee as IncompleteExpressionError;
				error = new IncompleteExpressionError (
					ee.Offset,
					ee.WasEOF,
					ee.Kind,
					wrap (iee?.IncompleteNode as T, ee.Offset)
				);
				return true;
			}
			success = (T)result;
			return false;
		}

		static ExpressionNode ParsePropertyInstanceFunction (string buffer, ref int offset, int endOffset, int baseOffset, ExpressionPropertyNode target)
		{
			offset++;

			ConsumeSpace (buffer, ref offset, endOffset);

			int nameOffset = offset;
			var funcNameStr = ReadName (buffer, ref offset, endOffset);
			if (funcNameStr == null) {
				return new IncompleteExpressionError (
					baseOffset + offset,
					offset > endOffset,
					ExpressionErrorKind.ExpectingMethodName,
					new ExpressionPropertyFunctionInvocation (target.Offset, offset + baseOffset - target.Offset, target, null, null));
			}
			var funcName = new ExpressionFunctionName (baseOffset + nameOffset, funcNameStr);

			ConsumeSpace (buffer, ref offset, endOffset);

			if (offset > endOffset) {
				return new IncompleteExpressionError (
					baseOffset + offset,
					offset > endOffset,
					ExpressionErrorKind.IncompleteProperty,
					new ExpressionPropertyFunctionInvocation (target.Offset, offset + baseOffset - target.Offset, target, funcName, null)
				);
			}

			//arguments are optional, it could be a property
			ExpressionArgumentList args = null;
			if (buffer [offset] == '(') {
				if (WrapError (
					ParseFunctionArgumentList (buffer, ref offset, endOffset, baseOffset),
					out args,
					out IncompleteExpressionError error,
					(n, o) => new ExpressionPropertyFunctionInvocation (target.Offset, o + baseOffset - target.Offset, target, funcName, n)
				)) {
					return error;
				}
			}

			return new ExpressionPropertyFunctionInvocation (target.Offset, offset + baseOffset - target.Offset, target, funcName, args);
		}

		static ExpressionNode ParsePropertyIndexer (string buffer, ref int offset, int endOffset, int baseOffset, ExpressionPropertyNode target)
		{
			if (WrapError (
					ParseFunctionArgumentList (buffer, ref offset, endOffset, baseOffset),
					out ExpressionArgumentList args,
					out IncompleteExpressionError error,
					(n, o) => new ExpressionPropertyFunctionInvocation (target.Offset, o + baseOffset - target.Offset, target, null, n)
				)) {
				return error;
			}
			return new ExpressionPropertyFunctionInvocation (target.Offset, offset + baseOffset - target.Offset, target, null, args);
		}

		static ExpressionNode ParseFunctionArgumentList (string buffer, ref int offset, int endOffset, int baseOffset)
		{
			bool isIndexer = buffer [offset] == '[';
			char terminator = isIndexer ? ']' : ')';

			int start = offset;

			offset++;
			ConsumeSpace (buffer, ref offset, endOffset);

			var values = new List<ExpressionNode> ();
			bool first = true;

			while (true) {
				if (offset <= endOffset && buffer [offset] == terminator) {
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

			if (ch == '"' || ch == '\'' || ch == '`') {
				return ReadArgumentString (ch, buffer, ref offset, endOffset, baseOffset);
			}

			if (ch == '$' && offset < endOffset && buffer [offset + 1] == '(') {
				offset += 2;
				var prop = ParseProperty (buffer, ref offset, endOffset, baseOffset);
				offset++;
				return prop;
			}

			if (char.IsLetter (ch)) {
				var crNode = ReadClassReference (buffer, ref offset, endOffset, baseOffset);
				if (crNode is ExpressionClassReference classRef && bool.TryParse (classRef.Name, out bool boolVal)) {
					return new ExpressionArgumentBool (classRef.Offset, classRef.Length, boolVal);
				}
				return crNode;
			}
			offset = start;

			return new ExpressionError (baseOffset + offset, wasFirst? ExpressionErrorKind.ExpectingRightParenOrValue : ExpressionErrorKind.ExpectingValue);
		}

		static ExpressionNode ReadArgumentString (char terminator, string buffer, ref int offset, int endOffset, int baseOffset)
		{
			int start = offset;
			offset++;
			while (offset <= endOffset) {
				char ch = buffer [offset];
				if (ch == terminator) {
					offset++;
					//FIXME wrap this in something that represents the quotes?
					return Parse (buffer, start + 1, offset - 2, ExpressionOptions.ItemsAndMetadata, baseOffset);
				}
				offset++;
			}

			var expr = Parse (buffer, start + 1, endOffset, ExpressionOptions.ItemsAndMetadata, baseOffset);
			if (expr is ExpressionError) {
				return expr;
			}

			return new IncompleteExpressionError (baseOffset + offset, true, ExpressionErrorKind.IncompleteString, expr);
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
