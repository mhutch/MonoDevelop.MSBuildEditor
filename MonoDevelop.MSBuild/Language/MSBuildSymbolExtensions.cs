// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

using MonoDevelop.MSBuild.Language.Typesystem;

namespace MonoDevelop.MSBuild.Language;

public static class MSBuildSymbolExtensions
{
	public static bool HasDefaultValue (this IHasDefaultValue symbol) => !string.IsNullOrEmpty (symbol.DefaultValue);

	public static bool HasDescription (this ISymbol symbol) => !symbol.Description.IsEmpty;

	public static bool IsDeprecated (this IDeprecatable symbol) => !string.IsNullOrEmpty (symbol.DeprecationMessage);

	public static bool IsDeprecated (this IDeprecatable symbol, [NotNullWhen (true)] out string? deprecationMessage)
	{
		if (IsDeprecated (symbol)) {
			deprecationMessage = symbol.DeprecationMessage;
			return true;
		}
		deprecationMessage = null;
		return false;
	}

	public static bool IsDeprecated (this ISymbol symbol) => symbol is IDeprecatable deprecatable && deprecatable.IsDeprecated ();

	public static bool IsDeprecated (this ISymbol symbol, [NotNullWhen (true)] out string? deprecationMessage)
	{
		if (symbol is IDeprecatable deprecatable && deprecatable.IsDeprecated ()) {
			deprecationMessage = deprecatable.DeprecationMessage;
			return true;
		}
		deprecationMessage = null;
		return false;
	}

	public static bool HasHelpUrl (this ISymbol symbol, [NotNullWhen (true)] out string? helpUrl)
	{
		if (symbol is IHasHelpUrl hasHelp && !string.IsNullOrEmpty (hasHelp.HelpUrl)) {
			helpUrl = hasHelp.HelpUrl;
			return true;
		}
		helpUrl = null;
		return false;
	}

	/// <summary>
	/// Checks whether the symbol's value kind is a specific value kind or list of that value kind.
	/// </summary>
	public static bool IsKindOrListOfKind (this ITypedSymbol typedSymbol, MSBuildValueKind compareTo) => typedSymbol.ValueKind.IsKindOrListOfKind (compareTo);

	/// <summary>
	/// Check whether the value allows expressions, i.e. the absence of the Literal modifier
	/// </summary>
	public static bool AllowsExpressions (this ITypedSymbol typedSymbol) => typedSymbol.ValueKind.AllowsExpressions ();

	/// <summary>
	/// Whether the type permits lists, i.e. whether it has a list modifier flag or is an unknown type. By default
	/// it only respects <see cref="MSBuildValueKind.ListSemicolon"/> but this can be overridden with <paramref name="listKind"/>.
	/// </summary>
	/// <param name="listKind">
	/// Which list modifiers to respect. Ignores bits other than <see cref="MSBuildValueKind.ListSemicolon"/>,
	/// <see cref="MSBuildValueKind.ListComma"/> or <see cref="MSBuildValueKind.ListSemicolonOrComma"/>.
	/// </param>
	public static bool AllowsLists (this ITypedSymbol typedSymbol, MSBuildValueKind listKind = MSBuildValueKind.ListSemicolon) => typedSymbol.ValueKind.AllowsLists (listKind);

	/// <summary>
	/// Returns the type without any modifier flags (i.e. list, literal)
	/// </summary>
	public static MSBuildValueKind ValueKindWithoutModifiers (this ITypedSymbol typedSymbol) => typedSymbol.ValueKind.WithoutModifiers ();

	/// <summary>
	/// Check whether the symbol is of the specified type or derived from it.
	/// </summary>
	public static bool IsKindOrDerived (this ITypedSymbol valueSymbol, MSBuildValueKind kind)
	{
		var actualKind = valueSymbol.ValueKindWithoutModifiers ();
		return actualKind == kind || (actualKind == MSBuildValueKind.CustomType && valueSymbol.CustomType?.BaseKind == kind);
	}
}