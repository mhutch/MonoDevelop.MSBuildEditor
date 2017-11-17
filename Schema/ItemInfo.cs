// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace MonoDevelop.MSBuildEditor.Schema
{
	class ItemInfo : BaseInfo
	{
		public ItemInfo (string name, string description)
			: this (name, description, null, MSBuildValueKind.Unknown, null)
		{
			
		}

		public ItemInfo (string name, string description, string includeDescription, MSBuildValueKind kind, Dictionary<string, MetadataInfo> metadata)
			: base (name, description)
		{
			ItemKind = kind;
			Metadata = metadata ?? new Dictionary<string, MetadataInfo> ();
			IncludeDescription = includeDescription;
		}

		public Dictionary<string,MetadataInfo> Metadata { get; private set; }

		public MSBuildValueKind ItemKind { get; }

		public string IncludeDescription { get; }
    }
}