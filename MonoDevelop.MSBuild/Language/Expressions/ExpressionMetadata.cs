// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Language.Expressions
{
	[DebuggerDisplay ("Metadata: {ItemName}.{MetadataName}")]
	public class ExpressionMetadata : ExpressionNode
	{
		public string ItemName { get; }
		public string MetadataName { get; }

		/// <summary>
		/// Whether the metadata uses the nonstandard qualified syntax %(a->b)
		/// </summary>
		public bool IsNonStandardSyntax { get; }

		public ExpressionMetadata (int offset, int length, string itemName, string metadataName, bool isNonStandardSyntax = false) : base (offset, length)
		{
			ItemName = itemName;
			MetadataName = metadataName;
			IsNonStandardSyntax = isNonStandardSyntax;
		}

		public bool IsQualified => ItemName != null;

		public int MetadataNameOffset => ItemName == null ? Offset + 2 : Offset + 3 + ItemName.Length;
		public int ItemNameOffset => Offset + 2;

		public TextSpan ItemNameSpan => new (Offset + 2, ItemName.Length);
		public TextSpan MetadataNameSpan => new (MetadataNameOffset, MetadataName.Length);

		public string GetItemName ()
		{
			if (ItemName != null) {
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

		public override ExpressionNodeKind NodeKind => ExpressionNodeKind.Metadata;
	}
}
