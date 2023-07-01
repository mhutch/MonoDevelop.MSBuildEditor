// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;

using MonoDevelop.MSBuild.Language.Typesystem;

using Newtonsoft.Json.Linq;

namespace MonoDevelop.MSBuild.Schema;

partial class MSBuildSchema
{
	struct TypeInfoReader
	{
		public TypeInfoReader (SchemaLoadState state, JObject parent, bool isItem)
		{
			this.state = state;
			this.parent = parent;
			this.isItem = isItem;

			packageType = null;
			kind = default;
			customType = null;

			// items are always lists unless specified otherwise
			modifiers = isItem ? MSBuildValueKind.ListSemicolon : 0;
		}

		readonly SchemaLoadState state;
		readonly JObject parent;
		private readonly bool isItem;
		string packageType;
		MSBuildValueKind kind;
		CustomTypeInfo customType;
		MSBuildValueKind modifiers;

		public bool TryHandle(string key, JToken token)
		{
			switch (key) {
			case "type":
				(var parsedKind, var resolvedCustomType) = state.ReadType (token);
				kind |= parsedKind;
				customType = resolvedCustomType;
				return true;
			case "packageType":
				packageType = token.Value<string> ();
				return true;
			case "isSingleton" when isItem:
				modifiers &= ~MSBuildValueKind.ListSemicolonOrComma;
				return true;
			case "isLiteral" when !isItem:
				modifiers |= MSBuildValueKind.Literal;
				return true;
			case "isList" when !isItem:
				// may have been set by listSeparators, so ignore that as it's more specific
				if ((modifiers & MSBuildValueKind.ListSemicolonOrComma) == 0) {
					modifiers |= MSBuildValueKind.ListSemicolon;
				}
				return true;
			case "listSeparators" when !isItem:
				// it may have been set by isList, so clear existing list modifiers as this is more specific
				modifiers &= ~MSBuildValueKind.ListSemicolonOrComma;
				foreach (var separator in token.Value<string> ()) {
					switch (separator) {
					case ';':
						modifiers |= MSBuildValueKind.ListSemicolon;
						break;
					case ',':
						modifiers |= MSBuildValueKind.ListComma;
						break;
					default:
						state.AddWarning (token ?? parent, $"Unsupported list separator char '{separator}'");
						break;
					}
				}
				return true;
			}
			return false;
		}

		public (MSBuildValueKind kind, CustomTypeInfo info) TryMaterialize ()
		{
			if (packageType != null) {
				if (kind != MSBuildValueKind.NuGetID) {
					state.AddWarning (parent, $"Property 'packageType' is invalid for kind '{kind}'");
				} else {
					// this is kinda hacky but we don't have anywhere else to put it.
					// ideally we would have some kind of general purpose annotation mechanism
					// but no point in designing it till we have other use cases
					customType = new CustomTypeInfo (new[] { new CustomTypeValue (packageType, null) });
				}
			}

			return (kind | modifiers, customType);
		}
	}

	static MSBuildValueKind? TryParseValueKind (string valueKind)
		=> valueKindNameMap.TryGetValue (valueKind, out var parsed) ? parsed : null;

	/// <summary>
	/// Gets the value kind names as used in the schema file format
	/// </summary>
	internal static IEnumerable<(string name, MSBuildValueKind kind)> GetValueKindNames ()
		=> valueKindNameMap.Select (kvp => (kvp.Key, kvp.Value));

	// NOTE: the order exactly mirrors the "valueTypeIntrinsic" section in the JSON schema, buildschema.json
	// Please keep them in sync.
	// Also try to keep DescriptionFormatter.FormatKind consistent with this.
	static readonly Dictionary<string, MSBuildValueKind> valueKindNameMap = new () {
		{ "data", MSBuildValueKind.Data },
		{ "bool", MSBuildValueKind.Bool },
		{ "int", MSBuildValueKind.Int },
		{ "string", MSBuildValueKind.String },
		{ "guid", MSBuildValueKind.Guid },
		{ "url", MSBuildValueKind.Url },
		{ "version", MSBuildValueKind.Version },
		{ "suffixed-version", MSBuildValueKind.SuffixedVersion },
		{ "lcid", MSBuildValueKind.Lcid },
		{ "culture", MSBuildValueKind.Culture },
		{ "target-name", MSBuildValueKind.TargetName },
		{ "item-name", MSBuildValueKind.ItemName },
		{ "property-name", MSBuildValueKind.PropertyName },
		{ "sdk", MSBuildValueKind.Sdk },
		{ "sdk-version", MSBuildValueKind.SdkVersion },
		{ "label", MSBuildValueKind.Label },
		{ "importance", MSBuildValueKind.Importance },
		{ "runtime-id", MSBuildValueKind.RuntimeID },
		{ "target-framework", MSBuildValueKind.TargetFramework },
		{ "target-framework-version", MSBuildValueKind.TargetFrameworkVersion },
		{ "target-framework-identifier", MSBuildValueKind.TargetFrameworkIdentifier },
		{ "target-framework-profile", MSBuildValueKind.TargetFrameworkProfile },
		{ "target-framework-moniker", MSBuildValueKind.TargetFrameworkMoniker },
		{ "nuget-id", MSBuildValueKind.NuGetID },
		{ "nuget-version", MSBuildValueKind.NuGetVersion },
		{ "project-file", MSBuildValueKind.ProjectFile },
		{ "file", MSBuildValueKind.File },
		{ "folder", MSBuildValueKind.Folder },
		{ "folder-with-slash", MSBuildValueKind.FolderWithSlash },
		{ "file-or-folder", MSBuildValueKind.FileOrFolder },
		{ "extension", MSBuildValueKind.Extension },
		{ "configuration", MSBuildValueKind.Configuration },
		{ "platform", MSBuildValueKind.Platform },
		{ "project-kind-guid", MSBuildValueKind.ProjectKindGuid }
	};
}
