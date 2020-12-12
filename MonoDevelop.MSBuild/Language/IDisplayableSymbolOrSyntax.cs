// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.MSBuild.Language.Typesystem;

namespace MonoDevelop.MSBuild.Language
{
	/// <summary>
	/// Common interface for syntax and symbols that may be displayed in tooltips or completion
	/// </summary>
	public interface IDisplayableSymbolOrSyntax
	{
		string Name { get; }
		DisplayText Description { get; }
	}

	/// <summary>
	/// Common interface for syntax and symbols that may be deprecated
	/// </summary>
	public interface IDeprecatable : IDisplayableSymbolOrSyntax
	{
		bool IsDeprecated { get; }
		string DeprecationMessage { get; }
	}

	public interface ITypedSymbol : IDisplayableSymbolOrSyntax
	{
		MSBuildValueKind ValueKind { get; }
		CustomTypeInfo CustomType { get; }
	}
}