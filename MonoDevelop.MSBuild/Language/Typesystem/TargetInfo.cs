// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MonoDevelop.MSBuild.Language.Typesystem;

public class TargetInfo (
	string name, DisplayText description, SymbolVersionInfo? versionInfo = null, string? helpUrl = null
	)
	: BaseSymbol(name, description), IVersionableSymbol, IHasHelpUrl
{
	public SymbolVersionInfo? VersionInfo { get; } = versionInfo;
	public string? HelpUrl { get; } = helpUrl;
}