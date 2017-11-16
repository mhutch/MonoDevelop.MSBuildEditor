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

			//construct a customized version of the include/exclude/etc attribute if appropriate
			if (info is MSBuildLanguageAttribute att) {
				switch (att.Name.ToLower ()) {
				case "Include":
				case "Exclude":
				case "Remove":
				case "Update":
					var item = ctx.GetSchemas ().GetItem (rr.ElementName);
					if (item != null && !string.IsNullOrEmpty (item.IncludeDescription)) {
						switch (item.ItemKind) {
						case MSBuildItemKind.File:
						case MSBuildItemKind.Folder:
							return GetDesc ($"Item.{att.Name}.ParameterizedFiles");
						case MSBuildItemKind.SingleFile:
						case MSBuildItemKind.SingleString:
							return GetDesc ($"Item.{att.Name}.ParameterizedSingle");
						default:
							return GetDesc ($"Item.{att.Name}.Parameterized");
						}
					}
					string GetDesc (string id) => string.Format (
						ElementDescriptions.ResourceManager.GetString (id, ElementDescriptions.Culture),
						item.IncludeDescription);

					break;
				}
			}

			return info.Description;
		}
	}
}
