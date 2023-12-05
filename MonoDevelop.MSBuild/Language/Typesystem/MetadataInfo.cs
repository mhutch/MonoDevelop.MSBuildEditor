// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MonoDevelop.MSBuild.Language.Typesystem;

public class MetadataInfo : VariableInfo
{
	public bool Reserved { get; }
	public bool Required { get; }

	public MetadataInfo (
		string name, DisplayText description,
		bool reserved = false, bool required = false, MSBuildValueKind valueKind = MSBuildValueKind.Unknown,
		ItemInfo? item = null, CustomTypeInfo? customType = null,
		string? defaultValue = null, string? deprecationMessage = null)
		: base (name, description, valueKind, customType, defaultValue, deprecationMessage)
	{
		Item = item;
		Required = required;
		Reserved = reserved;
	}

	public ItemInfo? Item { get; internal set; }
}