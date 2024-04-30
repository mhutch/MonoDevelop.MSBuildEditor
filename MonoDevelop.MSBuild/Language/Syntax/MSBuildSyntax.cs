// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;

using MonoDevelop.MSBuild.Language.Typesystem;

namespace MonoDevelop.MSBuild.Language.Syntax;

public abstract class MSBuildSyntax : ISymbol, ITypedSymbol, IVersionableSymbol, IHasHelpUrl
{
	protected MSBuildSyntax (
		string name, DisplayText description, MSBuildValueKind valueKind = MSBuildValueKind.Unknown,
		CustomTypeInfo? customType = null,
		SymbolVersionInfo? versionInfo = null,
		string? helpUrl = null)
	{
		Name = name;
		Description = description;
		VersionInfo = versionInfo;
		HelpUrl = helpUrl;

		ValueKind = valueKind;
		CustomType = customType;
	}

	public string Name { get; }
	public DisplayText Description { get; }

	public virtual MSBuildValueKind ValueKind { get; }
	public CustomTypeInfo? CustomType { get; }
	public SymbolVersionInfo? VersionInfo { get; }
	public virtual string? HelpUrl { get; }
}