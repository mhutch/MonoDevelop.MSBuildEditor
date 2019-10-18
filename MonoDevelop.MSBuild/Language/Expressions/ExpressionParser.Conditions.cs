using System;
using System.Collections.Generic;
using System.Text;

namespace MonoDevelop.MSBuild.Language.Expressions
{
	partial class ExpressionParser
	{
		public static ExpressionNode ParseCondition (string expression, int baseOffset = 0)
		{
			return ParseCondition (expression, 0, expression.Length - 1, baseOffset);
		}

		public static ExpressionNode ParseCondition (string buffer, int startOffset, int endOffset, int baseOffset)
		{
			var expr = ParseCondition (buffer, ref startOffset, endOffset, baseOffset, out bool hasError);

			if (!hasError) {
				ConsumeSpace (buffer, ref startOffset, endOffset);
				if (startOffset <= endOffset) {
					return new IncompleteExpressionError (startOffset, false, ExpressionErrorKind.UnexpectedCharacter, expr);
				}
			}

			return expr;
		}

		static ExpressionNode ParseCondition (string buffer, ref int offset, int endOffset, int baseOffset, out bool hasError)
		{
			ConsumeSpace (buffer, ref offset, endOffset);
			int start = offset;

			if (offset > endOffset) {
				hasError = false;
				return null;
			}

			char ch = buffer[offset];

			if (ch == '!') {
				offset++;
				var operand = ParseConditionOperand (buffer, ref offset, endOffset, baseOffset, out hasError);
				return new ExpressionConditionOperator (baseOffset + start, offset - start, ExpressionOperatorKind.Not, operand, null);
			}

			var left = ParseConditionOperand (buffer, ref offset, endOffset, baseOffset, out hasError);

			//todo: errors

			ConsumeSpace (buffer, ref offset, endOffset);
			if (offset >= endOffset) {
				return left;
			}

			ExpressionNode right;
			var op = ReadOperator (buffer, ref offset, endOffset, out var opError);
			if (opError != null) {
				right = opError;
			} else {
				ConsumeSpace (buffer, ref offset, endOffset);
				right = ParseConditionOperand (buffer, ref offset, endOffset, baseOffset, out hasError);
			}

			return new ExpressionConditionOperator (baseOffset + start, offset - start, op, left, right);
		}

		static char? TryReadEntity (string buffer, ref int offset, int endOffset)
		{
			offset++;
			var id = TryReadAlphaName (buffer, ref offset, endOffset);
			if (id != null && offset < endOffset && buffer[offset] == ';') {
				offset++;
				return id switch
				{
					"gt" => '>',
					"lt" => '>',
					"quot" => '"',
					"apos" => '\'',
					"amp" => '&',
					_ => null
				};
			}
			return null;
		}

		static ExpressionOperatorKind ReadOperator (string buffer, ref int offset, int endOffset, out ExpressionError error)
		{
			error = null;
			int start = offset;
			char ch = buffer[offset];

			if (ch == '&') {
				if (TryReadEntity (buffer, ref offset, endOffset) is char ec) {
					ch = ec;
				} else {
					throw new Exception ("Incomplete/unsupported entity");
				}
			}

			switch (ch) {
			case '=': {
					offset++;
					if (offset < endOffset && buffer[offset] == '=') {
						offset++;
						return ExpressionOperatorKind.Equal;
					}
					throw new Exception ("Expecting =");
				}
			case '>': {
					offset++;
					if (offset < endOffset && buffer[offset] == '=') {
						offset++;
						return ExpressionOperatorKind.GreaterThanOrEqual;
					}
					return ExpressionOperatorKind.GreaterThan;
				}
			case '<': {
					offset++;
					if (offset < endOffset && buffer[offset] == '=') {
						offset++;
						return ExpressionOperatorKind.LessThanOrEqual;
					}
					return ExpressionOperatorKind.LessThan;
				}
			case '!': {
					offset++;
					if (offset < endOffset && buffer[offset] == '=') {
						offset++;
						return ExpressionOperatorKind.NotEqual;
					}
					throw new Exception ("Expecting =");
				}
			case 'A':
			case 'a':
			case 'O':
			case 'o':
				offset--;
				switch (TryReadAlphaName (buffer, ref offset, Math.Min (offset + 4, endOffset))?.ToUpper ()) {
				case "AND": return ExpressionOperatorKind.And;
				case "OR": return ExpressionOperatorKind.Or;
				}
				break;
			}

			offset = start;
			throw new Exception ("Unknown");
		}

		static ExpressionNode ParseConditionOperand (string buffer, ref int offset, int endOffset, int baseOffset, out bool hasError)
		{
			hasError = false;

			int start = offset;

			var ch = buffer[start];
			if (ch == '.' || char.IsDigit (ch)) {
				return ReadArgumentNumber (buffer, ref offset, endOffset, baseOffset, out hasError);
			}

			if (ch == '"' || ch == '\'' || ch == '`') {
				return ReadArgumentString (ch, buffer, ref offset, endOffset, baseOffset, out hasError);
			}

			bool wasEOF = false;

			if (ch == '$' || ch == '%') {
				if (offset < endOffset) {
					if (buffer[offset + 1] == '(') {
						offset += 2;
						ExpressionNode node = ch == '$'
							? ParseProperty (buffer, ref offset, endOffset, baseOffset, out hasError)
							: ParseMetadata (buffer, ref offset, endOffset, baseOffset, out hasError);
						offset++;
						return node;
					}
				} else {
					wasEOF = true;
				}
			} else if (char.IsLetter (ch)) {
				var name = TryReadAlphaName (buffer, ref offset, endOffset);
				if (bool.TryParse (name, out bool boolVal)) {
					hasError = false;
					return new ExpressionArgumentBool (baseOffset + start, name.Length, boolVal);
				} else {
					// functions
					throw new NotSupportedException ();
				}
			}

			//the token didn't start with any character we expected
			//consume the character anyway so the completion trigger can use it
			offset++;
			var expr = new ExpressionText (baseOffset + start, buffer.Substring (start, offset - start), true);
			var kind = ExpressionErrorKind.ExpectingRightParenOrValue;// : ExpressionErrorKind.ExpectingValue;
			return new IncompleteExpressionError (baseOffset + start, wasEOF, kind, expr);
		}
	}
}
