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

	class ExpressionLiteral : ExpressionNode
	{
		public string Value { get; }
		public string GetUnescapedValue () => XmlEscaping.UnescapeEntities (Value);
		public bool IsPure { get; }

		public ExpressionLiteral (int offset, string value, bool isPure) : base (offset, value.Length)
		{
			Value = value;
			IsPure = isPure;
		}
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
		public string Name { get; }
		public ExpressionNode Transform { get; }

		public ExpressionItem (int offset, int length, string name) : this (offset, length, name, null)
		{
		}

		public ExpressionItem (int offset, int length, string name, ExpressionNode transform) : base (offset, length)
		{
			Transform = transform;
			transform?.SetParent (this);
			Name = name;
		}

		public int NameOffset => Offset + 2;
		public bool HasTransform => Transform != null;
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
		PropertyFunctionsNotSupported,
		ExpectingMethodName,
		ExpectingLeftParen,
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
			MethodName = methodName;
			Arguments = arguments;
		}
	}

	class ExpressionArgumentList : ExpressionNode
	{
		public List<ExpressionNode> Arguments { get; }

		public ExpressionArgumentList(int offset, int length, List<ExpressionNode> arguments) : base (offset, length)
		{
			Arguments = arguments;
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
				if (item.HasTransform) {
					foreach (var n in item.Transform.WithAllDescendants ()) {
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
				if (item.HasTransform && item.Transform.ContainsOffset (offset)) {
					return item.Transform.FindInternal (offset);
				}
				break;
			case ExpressionProperty prop:
				if (prop.Expression != null && prop.Expression.ContainsOffset (offset)) {
					return prop.Expression.FindInternal (offset);
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
			}
			return node;
		}

		public static string GetMessage (this ExpressionErrorKind errorKind, ValueInfo info, out bool isWarning)
		{
			isWarning = false;
			switch (errorKind) {
			case ExpressionErrorKind.MetadataDisallowed:
				return $"{Name()} does not allow metadata";
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
				return $"{Name()} does not allow metadata";
			default:
				return $"Invalid expression: {errorKind}";
			}

			string Name () => DescriptionFormatter.GetTitleCaseKindName (info);
		}
	}
}
