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
				case "include":
				case "exclude":
				case "remove":
				case "update":
					var item = ctx.GetSchemas ().GetItem (rr.ElementName);
					if (item != null && !string.IsNullOrEmpty (item.IncludeDescription)) {
						switch (item.ValueKind) {
						case MSBuildValueKind.File:
						case MSBuildValueKind.Folder:
						case MSBuildValueKind.FolderWithSlash:
						case MSBuildValueKind.FileOrFolder:
							return GetDesc ($"Item.{att.Name}.ParameterizedFiles");
							default:
							if (!item.ValueKind.AllowLists ()) {
								return GetDesc ($"Item.{att.Name}.ParameterizedSingle");
							}
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

		public static (string kind, string name) GetTitle (BaseInfo info, MSBuildResolveResult rr)
		{
			switch (info) {
			case MSBuildLanguageElement el:
				if (!el.IsAbstract)
					return ("keyword", info.Name);
				break;
			case MSBuildLanguageAttribute att:
				if (!att.IsAbstract) {
					return ("keyword", info.Name);
				}
				break;
			case ItemInfo item:
				return ("item", info.Name);
			case PropertyInfo prop:
				return ("property", info.Name);
			case TargetInfo prop:
				return ("target", info.Name);
			case MetadataInfo meta:
				return (
					"metadata",
					rr.ReferenceItemName != null ? $"{rr.ReferenceItemName}.{info.Name}" : info.Name
				);
			case TaskInfo task:
				return ("task", info.Name);
			case ConstantInfo value:
				return ("value", info.Name);
			}
			return (null, null);
		}
	}
}
