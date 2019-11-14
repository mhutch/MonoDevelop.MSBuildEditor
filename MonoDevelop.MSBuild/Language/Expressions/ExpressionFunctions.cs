// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Diagnostics;

namespace MonoDevelop.MSBuild.Language.Expressions
{
	public class ExpressionArgumentList : ExpressionNode
	{
		public List<ExpressionNode> Arguments { get; }

		public ExpressionArgumentList (int offset, int length, params ExpressionNode[] arguments)
			: this (offset, length, new List<ExpressionNode> (arguments)) { }

		public ExpressionArgumentList (int offset, int length, List<ExpressionNode> arguments) : base (offset, length)
		{
			Arguments = arguments;
			foreach (var a in arguments) {
				a.SetParent (this);
			}
		}

		public override ExpressionNodeKind NodeKind => ExpressionNodeKind.ArgumentList;
	}

	public class ExpressionFunctionName : ExpressionNode
	{
		public string Name { get; }

		public ExpressionFunctionName (int offset, string name) : base (offset, name.Length)
		{
			Name = name;
		}

		public override ExpressionNodeKind NodeKind => ExpressionNodeKind.FunctionName;
	}

	public abstract class ExpressionArgumentLiteral : ExpressionNode
	{
		public object Value { get; }
		public abstract ExpressionArgumentLiteralKind Kind { get; }
		protected ExpressionArgumentLiteral(int offset, int length, object value) : base (offset, length)
		{
			Value = value;
		}
	}

	[DebuggerDisplay ("Literal: {Value}")]
	public abstract class ExpressionArgumentLiteral<T> : ExpressionArgumentLiteral
	{
		public new T Value => (T)base.Value;
		protected ExpressionArgumentLiteral (int offset, int length, T value) : base (offset, length, value) { }
	}

	public class ExpressionArgumentBool : ExpressionArgumentLiteral<bool>
	{
		public override ExpressionArgumentLiteralKind Kind => ExpressionArgumentLiteralKind.Bool;
		public ExpressionArgumentBool (int offset, bool value) : base (offset, value? 4 : 5, value) { }

		public override ExpressionNodeKind NodeKind => ExpressionNodeKind.ArgumentLiteralBool;
	}

	public class ExpressionArgumentInt : ExpressionArgumentLiteral<long>
	{
		public override ExpressionArgumentLiteralKind Kind => ExpressionArgumentLiteralKind.Int;
		public ExpressionArgumentInt (int offset, int length, long value) : base (offset, length, value) { }

		public override ExpressionNodeKind NodeKind => ExpressionNodeKind.ArgumentLiteralInt;
	}

	public class ExpressionArgumentFloat : ExpressionArgumentLiteral<double>
	{
		public override ExpressionArgumentLiteralKind Kind => ExpressionArgumentLiteralKind.Float;
		public ExpressionArgumentFloat (int offset, int length, double value) : base (offset, length, value) { }

		public override ExpressionNodeKind NodeKind => ExpressionNodeKind.ArgumentLiteralFloat;
	}

	public class ExpressionArgumentString : ExpressionArgumentLiteral<string>
	{
		public override ExpressionArgumentLiteralKind Kind => ExpressionArgumentLiteralKind.String;
		public ExpressionArgumentString (int offset, int length, string value) : base (offset, length, value) { }

		public override ExpressionNodeKind NodeKind => ExpressionNodeKind.ArgumentLiteralString;
	}

	public enum ExpressionArgumentLiteralKind
	{
		String,
		Int,
		Float,
		Bool
	}
}
