// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace MonoDevelop.MSBuild.Language;

/// <summary>
/// Common interface for symbols that may be deprecated or contain information about when they were introduced
/// </summary>
public interface IVersionableSymbol : ISymbol
{
	SymbolVersionInfo? VersionInfo { get; }
}

/// <summary>
/// Contains information about whether a symbol is deprecated and/or when it was introduced.
/// </summary>
/// <param name="DeprecationMessage">If the symbol is deprecated, the deprecation message, otherwise <c>null</c>. Zero-length deprecation messages are not permitted and will be ignored.</param>
/// <param name="DeprecatedInVersion">Optionally indicate the MSBuild version in which it was deprecated. Ignored if <see cref="DeprecationMessage"/> is <c>null</c>.</param>
/// <param name="IntroducedInVersion">Optionally indicate the MSBuild version in which the symbol was introduced.</param>
/// <param name="VersionKind">Indicates what kind of version was provided in <see cref="DeprecatedInVersion"/> and/or <see cref="IntroducedInVersion"/></param>
public record SymbolVersionInfo (string? DeprecationMessage = null, Version? DeprecatedInVersion = null, Version? IntroducedInVersion = null, SymbolVersionKind VersionKind = SymbolVersionKind.MSBuild)
{
	public bool IsDeprecated => !string.IsNullOrEmpty (DeprecationMessage); // TODO: ctor should throw on zero length deprecation messages

	public static SymbolVersionInfo Deprecated(string deprecationMessage) => new (DeprecationMessage: deprecationMessage);
	public static SymbolVersionInfo Deprecated(int majorVersion, int minorVersion, string deprecationMessage) => new (DeprecationMessage: deprecationMessage, DeprecatedInVersion: new Version (majorVersion, minorVersion));

	public static SymbolVersionInfo Introduced (int majorVersion, int minorVersion) => new (IntroducedInVersion: new Version(majorVersion, minorVersion));
}

/// <summary>
/// Indicates what kind of version was provided in the <see cref="SymbolVersionInfo"/>
/// </summary>
public enum SymbolVersionKind
{
	/// <summary>
	/// The symbol was deprecated or introduced in a specific version of MSBuild.
	/// </summary>
	MSBuild = 0,
	/// <summary>
	/// NOT SUPPORTED YET. The symbol was deprecated or introduced in a specific version of the .NET SDK.
	/// </summary>
	DotNetSdk,
	/// <summary>
	/// NOT SUPPORTED YET. The symbol was deprecated or introduced in a specific version of a NuGet package.
	/// </summary>
	NuGetPackage
}