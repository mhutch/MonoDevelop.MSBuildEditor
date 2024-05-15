// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System.Diagnostics.CodeAnalysis;

using MonoDevelop.MSBuild.Util;

namespace MonoDevelop.MSBuild.Evaluation
{
	/// <summary>
	/// A value that has been escaped and evaluated
	/// </summary>
	readonly struct EvaluatedValue
	{
		public string? EscapedValue { get; }

		[MemberNotNullWhen(false, nameof (EscapedValue))]
		public readonly bool IsNullOrEmpty => string.IsNullOrEmpty (EscapedValue);

		public EvaluatedValue (string escapedValue) => EscapedValue = escapedValue;

		public static EvaluatedValue FromUnescaped (string? unescapedValue) => unescapedValue is null? new() : new (MSBuildEscaping.EscapeString (unescapedValue));
		public static EvaluatedValue FromNativePath (string? nativePath) => nativePath is null ? new () : new (MSBuildEscaping.ToMSBuildPath (nativePath));

		public string? ToNativePath () => EscapedValue is null ? null : MSBuildEscaping.UnescapePath (EscapedValue);
		public string? Unescape () => EscapedValue is null ? null : MSBuildEscaping.UnescapePath (EscapedValue);
	}
}