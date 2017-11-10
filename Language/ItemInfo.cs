// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. ALl rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace MonoDevelop.MSBuildEditor.Language
{
	class ItemInfo : BaseInfo
	{
		public Dictionary<string,MetadataInfo> Metadata { get; private set; }

		public ItemInfo (string name, string description)
			: base (name, description)
		{
			Metadata = new Dictionary<string, MetadataInfo> ();
		}
	}
}