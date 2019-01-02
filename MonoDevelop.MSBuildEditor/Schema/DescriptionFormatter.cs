// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using MonoDevelop.MSBuildEditor.Language;

namespace MonoDevelop.MSBuildEditor.Schema
{
	static class DescriptionFormatter
	{
		public static DisplayText GetDescription (BaseInfo info, MSBuildDocument doc, MSBuildResolveResult rr)
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

			if (info.Description.IsEmpty) {
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
				return ("property", info.Name);
			case TargetInfo prop:
				return ("target", info.Name);
			case MetadataInfo meta:
				return ( "metadata", meta.Item != null ? $"{meta.Item.Name}.{info.Name}" : info.Name);
			case TaskInfo task:
				return ("task", info.Name);
			case ConstantInfo value:
				return ("value", info.Name);
			case FileOrFolderInfo value:
				return (value.IsFolder? "folder" : "file", info.Name);
			case FrameworkInfo fxi:
				return ("framework", fxi.Reference.DotNetFrameworkName);
			case TaskParameterInfo tpi:
				return ("parameter", tpi.Name);
			case FunctionInfo fi:
				return ("function", fi.Name);
			}
			return (null, null);
		}

		public static List<string> GetTypeDescription (this MSBuildValueKind kind)
		{
			var modifierList = new List<string> ();
			string kindName = FormatKind (kind);

			if (kindName != null) {
				modifierList.Add (kindName);
				if (kind.AllowLists ()) {
					modifierList.Add ("list");
				} else if (kind.AllowCommaLists ()) {
					modifierList.Add ("comma-list");
				}
				if (!kind.AllowExpressions ()) {
					modifierList.Add ("literal");
				}
			}

			return modifierList;
		}

		public static List<string> GetTypeDescription (this ValueInfo info)
		{
			var kind = MSBuildCompletionExtensions.InferValueKindIfUnknown (info);

			var modifierList = GetTypeDescription (kind);

			if (info.Values != null && info.Values.Count > 0) {
				modifierList [0] = "enum";
			}

			if (info is PropertyInfo pi && pi.Reserved) {
				modifierList.Add ("reserved");
			}
			if (info is MetadataInfo mi) {
				if (mi.Reserved) {
					modifierList.Add ("reserved");
				}
				if (mi.Required) {
					modifierList.Add ("required");
				}
			}

			if (info is TaskParameterInfo tpi) {
				if (tpi.IsOutput) {
					modifierList.Add ("output");
				}
				if (tpi.IsRequired) {
					modifierList.Add ("required");
				}
			}

			return modifierList;
		}

		static string FormatKind (MSBuildValueKind kind)
		{
			switch (kind.GetScalarType ()) {
			case MSBuildValueKind.Bool:
				return "bool";
			case MSBuildValueKind.Int:
				return "int";
			case MSBuildValueKind.String:
				return "string";
			case MSBuildValueKind.Guid:
				return "guid";
			case MSBuildValueKind.Url:
				return "url";
			case MSBuildValueKind.Version:
				return "version";
			case MSBuildValueKind.SuffixedVersion:
				return "version-suffixed";
			case MSBuildValueKind.Lcid:
				return "lcid";
			case MSBuildValueKind.DateTime:
				return "datetime";
			case MSBuildValueKind.Char:
				return "char";
			case MSBuildValueKind.Object:
				return "object";
			case MSBuildValueKind.Float:
				return "float";

			case MSBuildValueKind.TargetName:
				return "target-name";
			case MSBuildValueKind.ItemName:
				return "item-name";
			case MSBuildValueKind.PropertyName:
				return "property-name";
			case MSBuildValueKind.MetadataName:
				return "metadata-name";

			case MSBuildValueKind.TaskName:
				return "type-name";
			case MSBuildValueKind.TaskAssemblyName:
				return "assembly-name";
			case MSBuildValueKind.TaskAssemblyFile:
				return "file-path";
			case MSBuildValueKind.TaskFactory:
			case MSBuildValueKind.TaskArchitecture:
			case MSBuildValueKind.TaskRuntime:
				return "enum";
			case MSBuildValueKind.TaskOutputParameterName:
				return null;
			case MSBuildValueKind.TaskParameterType:
				return "type-name";
			case MSBuildValueKind.Sdk:
				return "sdk-id";
			case MSBuildValueKind.SdkVersion:
				return "sdk-version";
			case MSBuildValueKind.SdkWithVersion:
				return "sdk-id-version";
			case MSBuildValueKind.Xmlns:
			case MSBuildValueKind.Label:
				return null;
			case MSBuildValueKind.ToolsVersion:
			case MSBuildValueKind.Importance:
			case MSBuildValueKind.ContinueOnError:
			case MSBuildValueKind.HostOS:
			case MSBuildValueKind.HostRuntime:
				return "enum";
			case MSBuildValueKind.Configuration:
				return "configuration";
			case MSBuildValueKind.Platform:
				return "platform";
			case MSBuildValueKind.RuntimeID:
				return "rid";
			case MSBuildValueKind.TargetFramework:
				return "framework";
			case MSBuildValueKind.TargetFrameworkIdentifier:
				return "framework-id";
			case MSBuildValueKind.TargetFrameworkVersion:
				return "framework-version";
			case MSBuildValueKind.TargetFrameworkProfile:
				return "framework-profile";
			case MSBuildValueKind.TargetFrameworkMoniker:
				return "framework-moniker";
			case MSBuildValueKind.ProjectFile:
				return "file-path";
			case MSBuildValueKind.File:
				return "file-path";
			case MSBuildValueKind.Folder:
				return "directory-path";
			case MSBuildValueKind.FolderWithSlash:
				return "directory-path-trailing-slash";
			case MSBuildValueKind.FileOrFolder:
				return "file-or-folder";
			case MSBuildValueKind.Extension:
				return "file-extension";
			case MSBuildValueKind.Filename:
				return "file-name";
			case MSBuildValueKind.MatchItem:
			case MSBuildValueKind.UnknownItem:
				return "item";
			case MSBuildValueKind.NuGetID:
				return "nuget-id";
			case MSBuildValueKind.NuGetVersion:
				return "nuget-version";
			case MSBuildValueKind.ProjectKindGuid:
				return "flavor-guid";
			}
			return null;
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
