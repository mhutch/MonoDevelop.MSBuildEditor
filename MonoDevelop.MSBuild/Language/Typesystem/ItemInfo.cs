// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System.Collections.Generic;
using System.Diagnostics;

namespace MonoDevelop.MSBuild.Language.Typesystem;

[DebuggerDisplay("ItemInfo({Name},nq)")]
public class ItemInfo (
	string name, DisplayText description, string? includeDescription = null,
	MSBuildValueKind valueKind = MSBuildValueKind.Unknown, CustomTypeInfo? customType = null,
	Dictionary<string, MetadataInfo>? metadata = null,
	SymbolVersionInfo? versionInfo = null,
	string? helpUrl = null)
	: VariableInfo(name, description, valueKind, customType, null, versionInfo), IHasHelpUrl
{
	public Dictionary<string, MetadataInfo> Metadata { get; } = metadata ?? new (System.StringComparer.OrdinalIgnoreCase);

	//custom description for the kinds of items in the include
	public string? IncludeDescription { get; } = includeDescription;

	public string? HelpUrl { get; } = helpUrl;
}