// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.MSBuild.Language.Typesystem;

namespace MonoDevelop.MSBuild.Language;

/// <summary>
/// Base interface symbols that may be displayed in tooltips or completion
/// </summary>
public interface ISymbol
{
	string Name { get; }
	DisplayText Description { get; }
}

/// <summary>
/// Common interface for symbols that may be deprecated
/// </summary>
public interface IDeprecatable : ISymbol
{
	string? DeprecationMessage { get; }
}

/// <summary>
/// Common interface for symbols that are typed
/// </summary>
public interface ITypedSymbol : ISymbol
{
	MSBuildValueKind ValueKind { get; }
	CustomTypeInfo? CustomType { get; }
}

/// <summary>
/// Common interface for symbols that may have a default value
/// </summary>
public interface IHasDefaultValue : ITypedSymbol
{
	public string? DefaultValue { get; }
}

/// <summary>
/// Common interface for symbols that may have a help URL
/// </summary>
public interface IHasHelpUrl : ISymbol
{
	public string? HelpUrl { get; }
}

/// <summary>
/// Common interface for symbols that have been inferred from the project
/// </summary>
public interface IInferredSymbol : ISymbol
{
}
