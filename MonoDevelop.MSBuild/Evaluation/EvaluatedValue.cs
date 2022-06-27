// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.MSBuild.Util;

namespace MonoDevelop.MSBuild.Evaluation
{
	/// <summary>
	/// A value that has been escaped and evaluated
	/// </summary>
	struct EvaluatedValue
	{
		public string EscapedValue { get; }
		public bool IsNullOrEmpty => string.IsNullOrEmpty (EscapedValue);

		public EvaluatedValue (string escapedValue) => EscapedValue = escapedValue;

		public static EvaluatedValue FromUnescaped (string unescapedValue) => new (MSBuildEscaping.EscapeString (unescapedValue));
		public static EvaluatedValue FromNativePath (string nativePath) => new (MSBuildEscaping.ToMSBuildPath (nativePath));

		public string ToNativePath () => MSBuildEscaping.UnescapePath (EscapedValue);
		public string Unescape () => MSBuildEscaping.UnescapePath (EscapedValue);
	}
}