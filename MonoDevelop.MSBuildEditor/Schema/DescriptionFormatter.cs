// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using MonoDevelop.MSBuildEditor.Language;

namespace MonoDevelop.MSBuildEditor.Schema
{
	static class DescriptionFormatter
	{
		public static string GetDescription (BaseInfo info, MSBuildDocument doc, MSBuildResolveResult rr)
		{
			if (doc == null) {
				return info.Description;
			}

			//construct a customized version of the include/exclude/etc attribute if appropriate
			if (info is MSBuildLanguageAttribute att) {
				switch (att.Name.ToLower ()) {
				case "include":
				case "exclude":
				case "remove":
				case "update":
					var item = doc.GetSchemas ().GetItem (rr.ElementName);
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

			if (info.Description == null) {
				switch (info) {
				case PropertyInfo prop:
					if (info.Name.EndsWith ("DependsOn", StringComparison.OrdinalIgnoreCase)) {
						var targetName = info.Name.Substring (0, info.Name.Length - "DependsOn".Length);
						return $"The targets that the {targetName} target depends on";
					}
					break;
				case FrameworkInfo fxi:
					return FrameworkInfoProvider.GetDescription (fxi.Reference);
				}
			}

			return info.Description;
		}

		public static (string kind, string name) GetTitle (BaseInfo info)
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
				return (prop.Reserved? "reserved property" : "property", info.Name);
			case TargetInfo prop:
				return ("target", info.Name);
			case MetadataInfo meta:
				return (
					meta.Reserved ? "reserved metadata" : "metadata",
					meta.Item != null ? $"{meta.Item.Name}.{info.Name}" : info.Name
				);
			case TaskInfo task:
				return ("task", info.Name);
			case ConstantInfo value:
				return ("value", info.Name);
			case FrameworkInfo fxi:
				return ("framework", fxi.Reference.GetMoniker ());
			}
			return (null, null);
		}

		public static string GetTitleCaseKindName (this ValueInfo info)
		{
			switch (info) {
			case MSBuildLanguageElement el:
				return $"Element '{info.Name}'";
			case MSBuildLanguageAttribute att:
				return $"Attribute '{info.Name}'";
			case ItemInfo item:
				return $"Item '{info.Name}'";
			case PropertyInfo prop:
				return $"Property '{info.Name}'";
			case MetadataInfo meta:
				return $"Metadata '{info.Name}'";
			case TaskParameterInfo tpi:
				return $"Parameter '{info.Name}'";
			}
			throw new Exception ($"Unhandled type {info.GetType ()}");
		}
	}
}
