// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace MonoDevelop.MSBuild.Language.Expressions
{
	[DebuggerDisplay ("Metadata: {ItemName}.{MetadataName}")]
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
