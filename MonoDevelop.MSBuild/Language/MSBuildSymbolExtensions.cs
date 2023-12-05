// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

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
		if (symbol is IDeprecatable deprecatable) {
			deprecationMessage = deprecatable.DeprecationMessage;
			return true;
		}
		deprecationMessage = null;
		return false;
	}
}