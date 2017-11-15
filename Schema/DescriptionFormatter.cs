// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.MSBuildEditor.Language;

namespace MonoDevelop.MSBuildEditor.Schema
{
	static class DescriptionFormatter
	{
		public static string GetDescription (BaseInfo info, MSBuildResolveContext ctx, MSBuildResolveResult rr)
		{
			if (ctx == null || rr == null) {
				return info.Description;
			}

			if (info is MSBuildLanguageAttribute att) {
				string paramDesc = null;
				switch (att.Name.ToLower ()) {
				case "include":
					paramDesc = ElementDescriptions.Item_Include_Parameterized;
					break;
				case "exclude":
					paramDesc = ElementDescriptions.Item_Exclude_Parameterized;
					break;
				case "remove":
					paramDesc = ElementDescriptions.Item_Remove_Parameterized;
					break;
				case "update":
					paramDesc = ElementDescriptions.Item_Update_Parameterized;
					break;
				}
				if (paramDesc != null) {
					var item = ctx.GetSchemas ().GetItem (rr.ElementName);
					if (item != null && !string.IsNullOrEmpty (item.IncludeDescription)) {
						return string.Format (paramDesc, item.IncludeDescription);
					}
				}
			}

			return info.Description;
		}
	}
}
