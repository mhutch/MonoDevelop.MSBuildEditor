// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MonoDevelop.MSBuild.Language.Typesystem;

public class TargetInfo (string name, DisplayText description, string? deprecationMessage = null) : BaseSymbol(name, description), IDeprecatable
{
	public string? DeprecationMessage { get; } = deprecationMessage;
}