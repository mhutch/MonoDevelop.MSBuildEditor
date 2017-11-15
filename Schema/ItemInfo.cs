// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace MonoDevelop.MSBuildEditor.Schema
{
	class ItemInfo : BaseInfo
	{
		public ItemInfo (string name, string description)
			: this (name, description, false, null)
		{
			
		}

		public ItemInfo (string name, string description, bool isFile, Dictionary<string, MetadataInfo> metadata)
			: base (name, description)
		{
			IsFile = isFile;
			Metadata = metadata ?? new Dictionary<string, MetadataInfo> ();
		}

		public Dictionary<string,MetadataInfo> Metadata { get; private set; }
		public bool IsFile { get; }
    }
}