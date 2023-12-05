// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System.Collections.Generic;

namespace MonoDevelop.MSBuild.Language.Typesystem;

public class ItemInfo (
	string name, DisplayText description, string? includeDescription = null,
	MSBuildValueKind valueKind = MSBuildValueKind.Unknown, CustomTypeInfo? customType = null,
	Dictionary<string, MetadataInfo>? metadata = null,
	string? deprecationMessage = null)
	: VariableInfo(name, description, valueKind, customType, null, deprecationMessage)
{
	public Dictionary<string, MetadataInfo> Metadata { get; } = metadata ?? new (System.StringComparer.OrdinalIgnoreCase);

	//custom description for the kinds of items in the include
	public string? IncludeDescription { get; } = includeDescription;
}