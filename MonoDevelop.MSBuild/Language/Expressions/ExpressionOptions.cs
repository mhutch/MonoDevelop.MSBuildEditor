// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace MonoDevelop.MSBuild.Language.Expressions
{
	[Flags]
	public enum ExpressionOptions
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
}
