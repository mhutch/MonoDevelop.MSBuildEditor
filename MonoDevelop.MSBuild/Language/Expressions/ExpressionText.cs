// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace MonoDevelop.MSBuild.Language.Expressions
{
	[DebuggerDisplay ("{Value} (IsPure: {IsPure})")]
	class ExpressionText : ExpressionNode
	{
		public string Value { get; }
		public string GetUnescapedValue () => XmlEscaping.UnescapeEntities (Value);

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
}
