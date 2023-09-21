// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

using MonoDevelop.Xml.Parser;

namespace MonoDevelop.MSBuild.Language.Expressions
{
	[DebuggerDisplay ("{Value} (IsPure: {IsPure})")]
	public class ExpressionText : ExpressionNode
	{
		public string Value { get; }

		/// <summary>
		/// Gets the unescaped value of this text, optionally trimming whitespace.
		/// </summary>
		/// <param name="trim">Whether to trim leading and trailing whitespace.</param>
		/// <param name="trimmedOffset">The offset of the text, taking optional trimming into account.</param>
		/// <param name="escapedLength">The length of the escaped text, taking optional trimming into account.</param>
		/// <returns>The unescaped value of this text, optionally trimmed.</returns>
		public string GetUnescapedValue (bool trim, out int trimmedOffset, out int escapedLength)
		{
			string value = Value;
			escapedLength = value.Length;
			trimmedOffset = Offset;

			if (trim) {
				int start = 0;
				while (start < value.Length && XmlChar.IsWhitespace (value[start])) {
					start++;
				}
				int end = value.Length - 1;
				while (end >= start && XmlChar.IsWhitespace (value[end])) {
					end--;
				}
				if (start > 0 || end < value.Length - 1) {
					escapedLength = end - start + 1;
					value = value.Substring (start, end - start + 1);
				}
				trimmedOffset = start + Offset;
			}

			return XmlEscaping.UnescapeEntities (value);
		}

		/// <summary>
		/// Indicates whether this exists by itself, as opposed to being concatenated with other values.
		/// </summary>
		public bool IsPure { get; }

		public ExpressionText (int offset, string value, bool isPure) : base (offset, value.Length)
		{
			Value = value;
			IsPure = isPure;
		}

		public override ExpressionNodeKind NodeKind => ExpressionNodeKind.Text;
	}

	public class QuotedExpression : ExpressionNode
	{
		public QuotedExpression (char quoteChar, ExpressionNode expression) : this (expression.Offset - 1, expression.Length + 2, quoteChar, expression)
		{
		}

		public QuotedExpression (int offset, int length, char quoteChar, ExpressionNode expression) : base (offset, length)
		{
			QuoteChar = quoteChar;
			Expression = expression;
			Expression.SetParent (this);
		}

		public char QuoteChar { get; }

		public ExpressionNode Expression { get; }

		public override ExpressionNodeKind NodeKind => ExpressionNodeKind.QuotedExpression;
	}
}
