// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MonoDevelop.MSBuild.Language.Typesystem;

public class TargetInfo (string name, DisplayText description, string? deprecationMessage = null, string? helpUrl = null)
	: BaseSymbol(name, description), IDeprecatable, IHasHelpUrl
{
	public string? DeprecationMessage { get; } = deprecationMessage;
	public string? HelpUrl { get; } = helpUrl;
}