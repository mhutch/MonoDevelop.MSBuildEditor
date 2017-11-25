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
		public bool IsPure { get; }

		public ExpressionLiteral (int offset, string value, bool isPure) : base (offset, value.Length)
		{
			Value = value;
			IsPure = isPure;
		}
	}

	class ExpressionProperty : ExpressionNode
	{
		public string Name { get; }

		public ExpressionProperty (int offset, int length, string name) : base (offset, length)
		{
			Name = name;
		}

		public int NameOffset => Offset + 2;
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

		public ExpressionError (int offset, ExpressionErrorKind kind) : base (offset, 1)
		{
			Kind = kind;
		}
	}

	enum ExpressionErrorKind
	{
		MetadataDisallowed,
		EmptyListEntry,
		ExpectingLeftParen,
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
			if (node is Expression expr) {
				foreach (var c in expr.Nodes) {
					foreach (var n in c.WithAllDescendants ()) {
						yield return n;
					}
				}
			}
			else if (node is ExpressionItem item && item.HasTransform) {
				foreach (var n in item.Transform.WithAllDescendants ()) {
					yield return n;
				}
			}
		}

		public static bool ContainsOffset (this ExpressionNode node, int offset)
		{
			return node.Offset <= offset && node.End > offset;
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
						return c.Find (offset);
					}
				}
				break;
			case ExpressionItem item:
				if (item.HasTransform && item.Transform.ContainsOffset (offset)) {
					return item.Transform.Find (offset);
				}
				break;
			}
			return node;
		}

		public static string GetMessage (this ExpressionErrorKind errorKind, ValueInfo info)
		{
			switch (errorKind) {
			case ExpressionErrorKind.MetadataDisallowed:
				return $"{Name()} does not allow metadata";
			case ExpressionErrorKind.EmptyListEntry:
				return $"Empty list value";
			case ExpressionErrorKind.ExpectingLeftParen:
				return $"Expecting '('";
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
