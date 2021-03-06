// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace MonoDevelop.MSBuild.Language.Typesystem
{
	class ItemInfo : VariableInfo
	{
		public ItemInfo (
			string name, DisplayText description, string includeDescription = null,
			MSBuildValueKind valueKind = MSBuildValueKind.Unknown, CustomTypeInfo customType = null,
			Dictionary<string, MetadataInfo> metadata = null,
			bool isDeprecated = false, string deprecationMessage = null)
			: base (name, description, valueKind, customType, null, isDeprecated, deprecationMessage)
		{
			Metadata = metadata ?? new Dictionary<string, MetadataInfo> (System.StringComparer.OrdinalIgnoreCase);
			IncludeDescription = includeDescription;
		}

		public Dictionary<string,MetadataInfo> Metadata { get; }

		//custom description for the kinds of items in the include
		public string IncludeDescription { get; }
    }
}