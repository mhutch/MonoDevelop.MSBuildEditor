// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace MonoDevelop.MSBuild.Language.Typesystem;

public sealed class CustomTypeValue (
	string name, DisplayText description, SymbolVersionInfo? versionInfo = null, string? helpUrl = null,
	IReadOnlyList<string>? aliases = null)
	: ISymbol, ITypedSymbol, IVersionableSymbol, IHasHelpUrl
{
	public CustomTypeInfo CustomType { get; private set; }

	public MSBuildValueKind ValueKind => MSBuildValueKind.CustomType;

	public string Name { get; } = name;

	public DisplayText Description { get; } = description;

	public SymbolVersionInfo? VersionInfo { get; } = versionInfo;

	public string? HelpUrl => helpUrl ?? CustomType?.HelpUrl;

	public IReadOnlyList<string>? Aliases { get; } = aliases;

	internal void SetParent (CustomTypeInfo parent) => CustomType = parent;
}