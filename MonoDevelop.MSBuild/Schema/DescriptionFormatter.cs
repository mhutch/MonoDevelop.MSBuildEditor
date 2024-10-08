// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;

using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Syntax;
using MonoDevelop.MSBuild.Language.Typesystem;

namespace MonoDevelop.MSBuild.Schema
{
	static class DescriptionFormatter
	{
		public static string GetDescription (ISymbol info, MSBuildDocument doc, MSBuildResolveResult rr)
		{
			if (doc == null) {
				return info.Description.Text;
			}

			//construct a customized version of the include/exclude/etc attribute if appropriate
			if (info is MSBuildAttributeSyntax att) {
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
							if (!item.ValueKind.AllowsLists ()) {
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
					return FrameworkInfoProvider.GetDisplayDescription (fxi.Reference);
				}
			}

			return info.Description.Text;
		}

		public static (string kind, string name) GetTitle (ISymbol info)
		{
			switch (info) {
			case MSBuildElementSyntax el:
				if (!el.IsAbstract)
					return ("keyword", info.Name);
				break;
			case MSBuildAttributeSyntax att:
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
			// as these are ITypedSymbol, the type gets appended later
			case CustomTypeValue:
			case ConstantSymbol:
				return ("value", info.Name);
			case FileOrFolderInfo value:
				return (value.IsFolder? "folder" : "file", info.Name);
			case FrameworkInfo fxi:
				return ("framework", FrameworkInfoProvider.GetDisplayTitle (fxi.Reference));
			case TaskParameterInfo tpi:
				return ("parameter", tpi.Name);
			case FunctionInfo fi:
				if (fi.IsProperty) {
					//FIXME: can we resolve the msbuild / .net property terminology overloading?
					return ("property", fi.Name);
				}
				return ("function", fi.Name);
			case ClassInfo ci:
				return ("class", ci.Name);
			}
			return (null, null);
		}

		public static List<string> GetTypeDescription (this MSBuildValueKind kind, CustomTypeInfo customTypeInfo = null)
		{
			var modifierList = new List<string> ();
			string kindName = FormatKind (kind, customTypeInfo);

			if (kindName != null) {
				modifierList.Add (kindName);
				if (kind.AllowsLists (MSBuildValueKind.ListSemicolon)) {
					modifierList.Add ("list");
				} else if (kind.AllowsLists (MSBuildValueKind.ListComma)) {
					modifierList.Add ("comma-list");
				}
				if (!kind.AllowsExpressions ()) {
					modifierList.Add ("literal");
				}
			}

			return modifierList;
		}

		public static List<string> GetTypeDescription (this ITypedSymbol info)
		{
			var kind = info.ValueKind;

			var modifierList = GetTypeDescription (kind, info.CustomType);

			if (info is PropertyInfo pi) {
				if (pi.IsReserved) {
					modifierList.Add ("reserved");
				} else if (pi.IsReadOnly) {
					modifierList.Add ("readonly");
				}
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

		//FIXME: make consistent with MSBuildSchema.valueKindNameMap
		static string FormatKind (MSBuildValueKind kind, CustomTypeInfo customTypeInfo)
		{
			switch (kind.WithoutModifiers ()) {
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
			case MSBuildValueKind.VersionSuffixed:
				return "version-suffixed";
			case MSBuildValueKind.Lcid:
				return "lcid";
			case MSBuildValueKind.Culture:
				return "culture";
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
			case MSBuildValueKind.CustomType:
				if (customTypeInfo != null && !customTypeInfo.IsAnonymous) {
					return customTypeInfo.Name;
				}
				// derived types inherit the name from the base type
				if (customTypeInfo.BaseKind != MSBuildValueKind.Unknown) {
					return FormatKind (customTypeInfo.BaseKind, null);
				}
				return "enum";
			case MSBuildValueKind.ClrNamespace:
				return "clr-namespace";
			case MSBuildValueKind.ClrType:
				return "clr-type";
			case MSBuildValueKind.ClrTypeName:
				return "clr-type-name";
			case MSBuildValueKind.WarningCode:
				return "warning-code";
			case MSBuildValueKind.CSharpType:
				return "csharp-type";
			}
			return null;
		}

		/// <summary>
		/// Gets a lowercase noun describing the element. Intended to be localized and substituted into localized strings.
		/// </summary>
		public static string GetKindNoun (this ISymbol info)
			=> info switch {
				MSBuildElementSyntax _ => "element",
				MSBuildAttributeSyntax _ => "attribute",
				ItemInfo _ => "item",
				PropertyInfo _ => "property",
				MetadataInfo _ => "metadata",
				TaskParameterInfo _ => "task parameter",
				_ => "value"
			};

		/// <summary>
		/// Gets a title case noun describing the element. Intended to be localized and substituted into localized strings.
		/// </summary>
		public static string GetTitleCaseKindNoun (this ISymbol info)
			=> info switch {
				MSBuildElementSyntax _ => "Element",
				MSBuildAttributeSyntax _ => "Attribute",
				ItemInfo _ => "Item",
				PropertyInfo _ => "Property",
				MetadataInfo _ => "Metadata",
				TaskParameterInfo _ => "Task parameter",
				_ => "Value"
			};

		/// <summary>
		/// Gets a shortened version of the description suitable for displaying as a hint suffix in completion lists.
		/// </summary>
		public static string GetCompletionHint (ISymbol symbol)
		{
			var description = symbol.Description.Text;
			if (description == null || description.Length == 0) {
				return description;
			}
			return CreateShortenedDescription (description, maxLength: 60);
		}

		/// <summary>
		/// Creates a shortened version of the description by hard breaking on sentence boundary, or
		/// soft breaking on word boundary and adding ellipsis.
		/// </summary>
		static string CreateShortenedDescription (string description, int maxLength)
		{
			char prevChar = '\0';

			// avoid allocating this unless we actually need it
			StringBuilder? descriptionNoBackticks = null;

			int softBreakPos = description.Length;
			bool shouldSoftBreak = false;

			for (int i = 0; i < description.Length; i++) {
				char ch = description[i];
				switch (ch) {
				case ' ':
					// hard break on sentence i.e. ". ", removing the period
					if (prevChar == '.') {
						if (descriptionNoBackticks is not null) {
							descriptionNoBackticks.Length--;
							return descriptionNoBackticks.ToString ();
						} else {
							return description.Substring (0, i - 1);
						}
					}
					// Soft beak on space between words.
					if (prevChar != ' ') {
						// Only record the last soft break 3 or more chars before the short description length,
						// so we have space for the ... ellipsis
						int potentialSoftBreak = (descriptionNoBackticks is not null) ? descriptionNoBackticks.Length : i;
						if ((potentialSoftBreak + 3) < maxLength || softBreakPos == description.Length) {
							softBreakPos = (descriptionNoBackticks is not null) ? descriptionNoBackticks.Length : i;
						}
					}
					break;
				// hard break on newline
				case '\n':
					return descriptionNoBackticks?.ToString () ?? description.Substring (0, i);
				// When we find a backtick, skip the char.
				// On the first backtick, initialize the stringbuilder with the string up to this point.
				// When the stringbuilder is non-null, this indicates we are stripping backticks.
				case '`':
					if (descriptionNoBackticks == null) {
						descriptionNoBackticks = new (description);
						descriptionNoBackticks.Length = i;
					}
					continue;
				}
				prevChar = ch;

				// if withoutBackticks is non-null, we are stripping backticks, so append the char to the backtick-stripped stringbuilder
				if (descriptionNoBackticks is not null) {
					descriptionNoBackticks.Append (ch);
					// if without backticks is too long, break the loop, we're gonna soft break
					if (descriptionNoBackticks.Length > maxLength) {
						shouldSoftBreak = true;
						break;
					}
				}
				// if we're not stripping backticks, and is too long, break the loop, we're gonna soft break
				else if (i >= maxLength) {
					shouldSoftBreak = true;
					break;
				}
			}

			// Soft break if we found a space to break on, and add ellipsis
			if (shouldSoftBreak) {
				if (descriptionNoBackticks is not null) {
					descriptionNoBackticks.Length = Math.Min (softBreakPos, descriptionNoBackticks.Length);
					descriptionNoBackticks.Append ("...");
					return descriptionNoBackticks.ToString ();
				}
				return description.Substring (0, softBreakPos) + "...";
			}

			if (prevChar == '.') {
				if (descriptionNoBackticks is not null) {
					descriptionNoBackticks.Length--;
					return descriptionNoBackticks.ToString ();
				}
				return description.Substring (0, description.Length - 1);
			}

			return descriptionNoBackticks?.ToString () ?? description;
		}
	}
}
