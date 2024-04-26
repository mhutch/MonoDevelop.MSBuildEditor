// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace MonoDevelop.MSBuild.Language.Typesystem;

[DebuggerDisplay("MetadataInfo({DebuggerName},nq)")]
public class MetadataInfo : VariableInfo, IHasHelpUrl
{
	[DebuggerHidden]
	string DebuggerName => Item?.Name is string itemName? $"{itemName}.{Name}" : null;

	public bool Reserved { get; }
	public bool Required { get; }

	public MetadataInfo (
		string name, DisplayText description,
		bool reserved = false, bool required = false, MSBuildValueKind valueKind = MSBuildValueKind.Unknown,
		ItemInfo? item = null, CustomTypeInfo? customType = null,
		string? defaultValue = null,
		SymbolVersionInfo? versionInfo = null,
		string? helpUrl = null)
		: base (name, description, valueKind, customType, defaultValue, versionInfo)
	{
		Item = item;
		Required = required;
		Reserved = reserved;
		HelpUrl = helpUrl;
	}

	public ItemInfo? Item { get; internal set; }

	public string? HelpUrl { get; }
}