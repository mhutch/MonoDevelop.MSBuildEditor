// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using MonoDevelop.MSBuildEditor.Schema;

namespace MonoDevelop.MSBuildEditor.Language
{
	abstract class ExpressionNode
	{
		public int Offset { get; }
		public int Length { get; }
		public int End => Offset + Length;
		public ExpressionNode Parent { get; private set; }

		protected ExpressionNode (int offset, int length)
		{
			Offset = offset;
			Length = length;
		}

		internal void SetParent (ExpressionNode parent) => Parent = parent;
    }

	class Expression : ExpressionNode
	{
		public IReadOnlyList<ExpressionNode> Nodes { get; }

		public Expression (int offset, int length, params ExpressionNode [] nodes) : base (offset, length)
		{
			Nodes = nodes;

			foreach (var n in nodes) {
				n.SetParent (this);
			}
		}
	}

	class ExpressionList : Expression
	{
		public ExpressionList (int offset, int length, params ExpressionNode [] nodes) : base (offset, length, nodes)
		{
		}
	}

	class ExpressionText : ExpressionNode
	{
		public string Value { get; }
		public string GetUnescapedValue () => XmlEscaping.UnescapeEntities (Value);
		public bool IsPure { get; }

		public ExpressionText (int offset, string value, bool isPure) : base (offset, value.Length)
		{
			Value = value;
			IsPure = isPure;
		}
	}

	abstract class ExpressionArgumentLiteral : ExpressionNode
	{
		public object Value { get; }
		public abstract LiteralKind Kind { get; }
		protected ExpressionArgumentLiteral(int offset, int length, object value) : base (offset, length)
		{
			Value = value;
		}
	}

	abstract class ExpressionArgumentLiteral<T> : ExpressionArgumentLiteral
	{
		public new T Value => (T)base.Value;
		protected ExpressionArgumentLiteral(int offset, int length, T value) : base (offset, length, value) {}
	}

	class ExpressionArgumentBool : ExpressionArgumentLiteral<bool>
	{
		public override LiteralKind Kind => LiteralKind.Bool;
		public ExpressionArgumentBool(int offset, int length, bool value) : base (offset, length, value) { }
	}

	class ExpressionArgumentInt : ExpressionArgumentLiteral<long>
	{
		public override LiteralKind Kind => LiteralKind.Int;
		public ExpressionArgumentInt(int offset, int length, long value) : base (offset, length, value) { }
	}

	class ExpressionArgumentFloat : ExpressionArgumentLiteral<double>
	{
		public override LiteralKind Kind => LiteralKind.Float;
		public ExpressionArgumentFloat(int offset, int length, double value) : base (offset, length, value) { }
	}

	class ExpressionArgumentString : ExpressionArgumentLiteral<string>
	{
		public override LiteralKind Kind => LiteralKind.Bool;
		public ExpressionArgumentString(int offset, int length, string value) : base (offset, length, value) { }
	}

	enum LiteralKind
	{
		String,
		Int,
		Float,
		Bool
	}

	class ExpressionProperty : ExpressionNode
	{
		public ExpressionNode Expression { get; }

		public bool IsSimpleProperty => Expression is ExpressionPropertyName;
		public string Name => (Expression as ExpressionPropertyName)?.Name;
		public int? NameOffset => (Expression as ExpressionPropertyName)?.Offset;

		public ExpressionProperty (int offset, int length, ExpressionNode expression) : base (offset, length)
		{
			Expression = expression;
			expression.SetParent (this);
		}

		public ExpressionProperty(int offset, int length, string name)
			: this (offset, length, new ExpressionPropertyName (offset + 2, name.Length, name))
		{
		}
	}

	class ExpressionMetadata : ExpressionNode
	{
		public string ItemName { get; }
		public string MetadataName { get; }

		public ExpressionMetadata (int offset, int length, string itemName, string metadataName) : base (offset, length)
		{
			ItemName = itemName;
			MetadataName = metadataName;
		}

		public bool IsQualified => ItemName != null;

		public int MetadataNameOffset => ItemName == null ? Offset + 2 : Offset + 3 + ItemName.Length;
		public int ItemNameOffset => Offset + 2;

		public string GetItemName ()
		{
			if (ItemName !=null) {
				return ItemName;
			}
			var p = Parent;
			while (p != null) {
				if (p is ExpressionItem i) {
					return i.Name;
				}
				p = p.Parent;
			}
			return null;
		}
	}

	class ExpressionItem : ExpressionNode
	{
		public ExpressionItemNode Expression { get; }

		public bool IsSimpleItem => Expression is ExpressionItemName;
		public string Name => Expression.ItemName;
		public int? NameOffset => Expression.ItemNameOffset;

		public ExpressionItem (int offset, int length, ExpressionItemNode expression) : base (offset, length)
		{
			Expression = expression;
			expression.SetParent (this);
		}

		public ExpressionItem (int offset, int length, string name)
			: this (offset, length, new ExpressionItemName (offset + 2, name.Length, name))
		{
		}
	}

	class ExpressionError : ExpressionNode
	{
		public ExpressionErrorKind Kind { get; }

		public ExpressionError (int offset, int length, ExpressionErrorKind kind) : base (offset, length)
		{
			Kind = kind;
		}

		public ExpressionError (int offset, ExpressionErrorKind kind) : this (offset, 1, kind)
		{
		}
	}

	class IncompleteExpressionError : ExpressionError
	{
		public ExpressionNode IncompleteNode { get; }
		public bool WasEOF => Length == 0;

		public IncompleteExpressionError (int offset, bool wasEOF, ExpressionErrorKind kind, ExpressionNode incompleteNode)
			: base (offset, wasEOF? 0 : 1, kind)
		{
			IncompleteNode = incompleteNode;
			incompleteNode.SetParent (this);
		}
	}

	enum ExpressionErrorKind
	{
		MetadataDisallowed,
		EmptyListEntry,
		ExpectingItemName,
		ExpectingRightParen,
		ExpectingRightParenOrPeriod,
		ExpectingPropertyName,
		ExpectingMetadataName,
		ExpectingMetadataOrItemName,
		ExpectingRightAngleBracket,
		ExpectingApos,
		ExpectingRightParenOrDash,
		ItemsDisallowed,
		ExpectingMethodName,
		ExpectingLeftParen,
		ExpectingRightParenOrComma,
		ExpectingRightParenOrValue,
		ExpectingValue,
		CouldNotParseNumber,
		IncompleteValue,
		ExpectingMethodOrTransform,
		ExpectingBracketColonColon,
		ExpectingClassName,
		ExpectingClassNameComponent
	}

	class ExpressionPropertyNode : ExpressionNode
	{
		public ExpressionPropertyNode(int offset, int length) : base (offset, length)
		{
		}
	}

	class ExpressionPropertyFunctionInvocation : ExpressionPropertyNode
	{
		public ExpressionPropertyNode Target { get; }
		public string MethodName { get; }
		public ExpressionArgumentList Arguments;

		public ExpressionPropertyFunctionInvocation(int offset, int length, ExpressionPropertyNode target, string methodName, ExpressionArgumentList arguments)
			: base (offset, length)
		{
			Target = target;
			target?.SetParent (this);
			MethodName = methodName;
			Arguments = arguments;
			arguments?.SetParent (this);
		}
	}

	class ExpressionArgumentList : ExpressionNode
	{
		public List<ExpressionNode> Arguments { get; }

		public ExpressionArgumentList(int offset, int length, List<ExpressionNode> arguments) : base (offset, length)
		{
			Arguments = arguments;
			foreach (var a in arguments) {
				a.SetParent (this);
			}
		}
	}

	class ExpressionPropertyName : ExpressionPropertyNode
	{
		public string Name { get; }

		public ExpressionPropertyName(int offset, int length, string name) : base (offset, length)
		{
			Name = name;
		}
	}

	class ExpressionClassReference : ExpressionPropertyNode
	{
		public string Name { get; }

		public ExpressionClassReference(int offset, int length, string name) : base (offset, length)
		{
			Name = name;
		}
	}

	abstract class ExpressionItemNode : ExpressionNode
	{
		public ExpressionItemNode (int offset, int length) : base (offset, length)
		{
		}

		public abstract string ItemName { get; }
		public abstract int ItemNameOffset { get; }
	}

	class ExpressionItemName : ExpressionItemNode
	{
		public string Name { get; }

		public ExpressionItemName (int offset, int length, string name) : base (offset, length)
		{
			Name = name;
		}

		public override string ItemName => Name;
		public override int ItemNameOffset => Offset;
	}

	class ExpressionItemFunctionInvocation : ExpressionItemNode
	{
		public ExpressionItemNode Target { get; }
		public string MethodName { get; }

		public override string ItemName => Target.ItemName;
		public override int ItemNameOffset => Target.ItemNameOffset;

		public ExpressionArgumentList Arguments;

		public ExpressionItemFunctionInvocation (int offset, int length, ExpressionItemNode target, string methodName, ExpressionArgumentList arguments)
			: base (offset, length)
		{
			Target = target;
			target.SetParent (this);
			MethodName = methodName;
			Arguments = arguments;
			arguments?.SetParent (this);
		}
	}

	class ExpressionItemTransform : ExpressionItemNode
	{
		public ExpressionItemNode Target { get; }
		public ExpressionNode Transform { get; }

		public override string ItemName => Target.ItemName;
		public override int ItemNameOffset => Target.ItemNameOffset;

		public ExpressionItemTransform (int offset, int length, ExpressionItemNode target, ExpressionNode transform)
			: base (offset, length)
		{
			Target = target;
			target.SetParent (this);
			Transform = transform;
			transform.SetParent (this);
		}
	}

	[Flags]
	enum ExpressionOptions
	{
		None = 0,
		Metadata = 1,
		Items = 1,
		Lists = 1 << 1,
		CommaLists = 1 << 2,
		ItemsAndMetadata = Items | Metadata,
		ItemsMetadataAndLists = Items | Metadata | Lists,
		ItemsAndLists = Items | Lists
	}

	static class ExpressionExtensions
	{
		public static IEnumerable<ExpressionNode> WithAllDescendants (this ExpressionNode node)
		{
			yield return node;

			switch (node) {
			case Expression expr:
				foreach (var c in expr.Nodes) {
					foreach (var n in c.WithAllDescendants ()) {
						yield return n;
					}
				}
				break;
			case ExpressionItem item:
				if (item.Expression != null) {
					foreach (var n in item.Expression.WithAllDescendants ()) {
						yield return n;
					}
				}
				break;
			case ExpressionProperty property:
				if (property.Expression != null) {
					foreach (var n in property.Expression.WithAllDescendants ()) {
						yield return n;
					}
				}
				break;
			case ExpressionPropertyFunctionInvocation invocation:
				if (invocation.Target != null) {
					foreach (var n in invocation.Target.WithAllDescendants ()) {
						yield return n;
					}
				}
				if (invocation.Arguments != null) {
					foreach (var n in invocation.Arguments.WithAllDescendants ()) {
						yield return n;
					}
				}
				break;
			case ExpressionArgumentList argumentList:
				if (argumentList.Arguments != null) {
					foreach (var a in argumentList.Arguments) {
						foreach (var n in a.WithAllDescendants ()) {
							yield return n;
						}
					}
				}
				break;
			case ExpressionItemFunctionInvocation invocation:
				if (invocation.Target != null) {
					foreach (var n in invocation.Target.WithAllDescendants ()) {
						yield return n;
					}
				}
				if (invocation.Arguments != null) {
					foreach (var n in invocation.Arguments.WithAllDescendants ()) {
						yield return n;
					}
				}
				break;
			case ExpressionItemTransform transform:
				if (transform.Target != null) {
					foreach (var n in transform.Target.WithAllDescendants ()) {
						yield return n;
					}
				}
				if (transform.Transform != null) {
					foreach (var n in transform.Transform.WithAllDescendants ()) {
						yield return n;
					}
				}
				break;
			}
		}

		public static bool ContainsOffset (this ExpressionNode node, int offset)
		{
			return node.Offset <= offset && node.End >= offset;
		}

		public static ExpressionNode Find (this ExpressionNode node, int offset)
		{
			return node.ContainsOffset (offset) ? FindInternal (node, offset) : null;
		}

		static ExpressionNode FindInternal (this ExpressionNode node, int offset)
		{
			switch (node) {
			case Expression expr:
				//TODO: binary search?
				foreach (var c in expr.Nodes) {
					if (c.ContainsOffset (offset)) {
						return c.FindInternal (offset);
					}
				}
				break;
			case ExpressionItem item:
				if (item.Expression != null && item.Expression.ContainsOffset (offset)) {
					return item.Expression.FindInternal (offset);
				}
				break;
			case ExpressionProperty prop:
				if (prop.Expression != null && prop.Expression.ContainsOffset (offset)) {
					return prop.Expression.FindInternal (offset);
				}
				break;
			case IncompleteExpressionError err:
				if (err.IncompleteNode != null && err.IncompleteNode.ContainsOffset (offset)) {
					return err.IncompleteNode.FindInternal (offset);
				}
				break;
			case ExpressionPropertyFunctionInvocation prop:
				if (prop.Target != null && prop.Target.ContainsOffset (offset)) {
					return prop.Target.FindInternal (offset);
				}
				if (prop.Arguments != null && prop.Arguments.ContainsOffset (offset)) {
					return prop.Arguments.FindInternal (offset);
				}
				break;
			case ExpressionArgumentList argumentList:
				if (argumentList.Arguments != null) {
					//TODO: binary search?
					foreach (var c in argumentList.Arguments) {
						if (c.ContainsOffset (offset)) {
							return c.FindInternal (offset);
						}
					}
				}
				break;
			case ExpressionItemFunctionInvocation invocation:
				if (invocation.Target != null && invocation.Target.ContainsOffset (offset)) {
					return invocation.Target.FindInternal (offset);
				}
				if (invocation.Arguments != null && invocation.Arguments.ContainsOffset (offset)) {
					return invocation.Arguments.FindInternal (offset);
				}
				break;
			case ExpressionItemTransform transform:
				if (transform.Target != null && transform.Target.ContainsOffset (offset)) {
					return transform.Target.FindInternal (offset);
				}
				if (transform.Transform != null && transform.Transform.ContainsOffset (offset)) {
					return transform.Transform.FindInternal (offset);
				}
				break;
			}
			return node;
		}

		public static string GetMessage (this ExpressionErrorKind errorKind, ValueInfo info, out bool isWarning)
		{
			isWarning = false;
			switch (errorKind) {
			case ExpressionErrorKind.MetadataDisallowed:
				return $"{Name ()} does not allow metadata";
			case ExpressionErrorKind.EmptyListEntry:
				isWarning = true;
				return $"Empty list value";
			case ExpressionErrorKind.ExpectingItemName:
				return $"Expecting item name";
			case ExpressionErrorKind.ExpectingRightParen:
				return $"Expecting ')'";
			case ExpressionErrorKind.ExpectingRightParenOrPeriod:
				return $"Expecting ')' or '.'";
			case ExpressionErrorKind.ExpectingPropertyName:
				return $"Expecting property name";
			case ExpressionErrorKind.ExpectingMetadataName:
				return $"Expecting metadata name";
			case ExpressionErrorKind.ExpectingMetadataOrItemName:
				return $"Expecting metadata or item name";
			case ExpressionErrorKind.ExpectingRightAngleBracket:
				return $"Expecting '>'";
			case ExpressionErrorKind.ExpectingApos:
				return $"Expecting single quote";
			case ExpressionErrorKind.ExpectingRightParenOrDash:
				return $"Expecting '-' or ')'";
			case ExpressionErrorKind.ItemsDisallowed:
				return $"{Name ()} does not allow metadata";
			case ExpressionErrorKind.ExpectingMethodOrTransform:
				return $"Expecting item function or transform";
			case ExpressionErrorKind.ExpectingMethodName:
				return "Expecting method name";
			case ExpressionErrorKind.ExpectingLeftParen:
				return "Expecting '('";
			case ExpressionErrorKind.ExpectingRightParenOrComma:
				return "Expecting ')' or ','";
			case ExpressionErrorKind.ExpectingRightParenOrValue:
				return "Expecting ',' or value";
			case ExpressionErrorKind.ExpectingValue:
				return "Expecting value";
			case ExpressionErrorKind.CouldNotParseNumber:
				return "Invalid numeric value";
			case ExpressionErrorKind.IncompleteValue:
				return "Incomplete value";
			case ExpressionErrorKind.ExpectingBracketColonColon:
				return "Expecting ']::'";
			case ExpressionErrorKind.ExpectingClassName:
				return "Expecting class name";
			case ExpressionErrorKind.ExpectingClassNameComponent:
				return "Incomplete class name";

			default:
				return $"Invalid expression: {errorKind}";
			}

			string Name () => DescriptionFormatter.GetTitleCaseKindName (info);
		}
	}
}
