// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using MonoDevelop.MSBuild.Language.Syntax;
using MonoDevelop.MSBuild.Language.Typesystem;

namespace MonoDevelop.MSBuild.Language;

static class MSBuildIdentifier
{
	/// <summary>
	/// Tries to read an MSBuild identifier from the <paramref name="buffer"/> between the <paramref name="offset"/> and <paramref name="endOffset"/>,
	/// and advance the <paramref name="offset"/> to the end of the identifier.
	/// </summary>
	public static string? TryRead (string buffer, ref int offset, int endOffset)
	{
		if (TryGetLength (buffer, offset, endOffset, out int length)) {
			string val = buffer.Substring (offset, length);
			offset += length;
			return val;
		}
		return null;
	}

	/// <summary>
	/// Tries to read an MSBuild identifier from the <paramref name="buffer"/> between the <paramref name="offset"/> and <paramref name="endOffset"/>.
	/// The <paramref name="offset"/> will be advanced to the end of the identifier.
	/// </summary>
	/// <param name="buffer"></param>
	/// <param name="offset"></param>
	/// <param name="endOffset"></param>
	/// <returns></returns>
	public static bool TryGetLength (string buffer, int offset, int endOffset, out int length)
	{
		length = 0;
		if (offset > endOffset) {
			return false;
		}

		int start = offset;
		char ch = buffer[offset];
		if (!IsIdentifierFirstChar (ch)) {
			return false;
		}
		offset++;

		char lastChar = ch;
		while (offset <= endOffset) {
			ch = buffer[offset];
			if (!IsIdentifierChar (ch)) {
				break;
			}
			offset++;
			lastChar = ch;
		}

		// Although a dash is a valid char for MSBuild identifiers, we will disallow the last char being a dash
		// as this will conflict with item transform handling.
		// We could probably allow this if we special cased it but I really can't see a good reason to do so.
		if (lastChar == '-') {
			offset--;
		}

		length = offset - start;
		return true;
	}

	static bool IsIdentifierChar (char ch) => char.IsLetterOrDigit (ch) || ch == '_' || ch == '-';
	static bool IsIdentifierFirstChar (char ch) => char.IsLetter (ch) || ch == '_';
	static bool IsPascalComponentStart (char ch) => char.IsUpper (ch);
	static bool IsPascalComponentChar (char ch) => char.IsLetterOrDigit (ch);

	public static bool IsValid (string identifier) => TryGetLength (identifier, 0, identifier.Length - 1, out int length) && length == identifier.Length;

	/// <summary>
	/// Gets a PascalCase prefix from the identifier
	/// </summary>
	public static bool TryGetPrefix (string identifier, [NotNullWhen(true)] out string? prefix)
	{
		prefix = null;

		int idx = 0;

		if (identifier.Length < 4) {
			// a prefix requires two components so at minimum "AaBa"
			return false;
		}

		// ignore the _ visibility convention prefix
		if (identifier[idx] == '_') {
			idx++;
		}

		// prefix must start with a PascalCase component start char
		if (!IsPascalComponentStart (identifier[idx])) {
			return false;
		}
		int prefixStart = idx;

		idx++;

		while (idx < identifier.Length) {
			char ch = identifier [idx];
			if (!IsPascalComponentChar (ch)) {
				return false;
			}
			// only return the prefix when we found the start of a
			// preceding PascalCase component, else it's not prefixing anything
			if (IsPascalComponentStart (ch)) {
				int prefixLength = idx - prefixStart;
				if (prefixLength == 0) {
					return false;
				}
				prefix = identifier.Substring (prefixStart, prefixLength);
				return true;
			}
			idx++;
		}

		return false;
	}

	/// <summary>
	/// Gets a PascalCase suffix from the identifier
	/// </summary>
	public static bool TryGetSuffix (string identifier, [NotNullWhen (true)] out string? suffix)
	{
		suffix = null;

		int endIdx = identifier.Length - 1;
		int idx = endIdx;
		if (idx < 0) {
			return false;
		}

		string? possibleSuffix = null;

		while (idx >= 0) {
			char ch = identifier[idx];
			if (!IsPascalComponentChar (ch)) {
				return false;
			}
			if (IsPascalComponentStart (ch)) {
				if (idx == endIdx) {
					// a suffix is more than just one uppercase char
					suffix = null;
					return false;
				}
				if (possibleSuffix == null) {
					possibleSuffix = identifier.Substring (idx, endIdx - idx + 1);
				} else {
					// only return the suffix when we found the start of a
					// preceding PascalCase component, else it's not suffixing anything
					suffix = possibleSuffix;
					return true;
				}
			}
			idx--;
		}

		suffix = null;
		return false;
	}

	/// <summary>
	/// Try to infer the value kind from an identifier.
	/// </summary>
	public static MSBuildValueKind InferValueKind (string name, MSBuildSyntaxKind syntaxKind)
	{
		if (syntaxKind == MSBuildSyntaxKind.Property || syntaxKind == MSBuildSyntaxKind.Metadata) {
			if (TryGetPrefix (name, out var prefix)) {
				if (knownPrefixes.TryGetValue (prefix, out var valuekind)) {
					return valuekind;
				}
			}
			if (TryGetSuffix (name, out var suffix)) {
				if (knownSuffixes.TryGetValue (suffix, out var valueKind)) {
					return valueKind;
				}
				// these suffixes have multiple PascalCase components so must be handled differently
				if (EndsWith ("DependsOn")) {
					return MSBuildValueKind.TargetName.AsList ();
				}
				if (EndsWith ("FileName")) {
					return MSBuildValueKind.Filename;
				}
			}
		}

		//make sure these work even if the common targets schema isn't loaded
		if (syntaxKind == MSBuildSyntaxKind.Property) {
			if (Equals ("Configuration")) {
				return MSBuildValueKind.Configuration;
			}
			if (Equals ("Platform")) {
				return MSBuildValueKind.Platform;
			}
		}

		if (syntaxKind == MSBuildSyntaxKind.Item) {
			return MSBuildValueKind.Unknown.AsList ();
		}

		return MSBuildValueKind.Unknown;

		bool EndsWith (string suffix) => name.EndsWith (suffix, StringComparison.OrdinalIgnoreCase);
		bool Equals (string value) => name.Equals (value, StringComparison.OrdinalIgnoreCase);
	}

	static readonly Dictionary<string, MSBuildValueKind> knownPrefixes = new () {
		{ "Enable", MSBuildValueKind.Bool },
		{ "Disable", MSBuildValueKind.Bool },
		{ "Require", MSBuildValueKind.Bool },
		{ "Use", MSBuildValueKind.Bool },
		{ "Allow", MSBuildValueKind.Bool },
		{ "Is", MSBuildValueKind.Bool },
		{ "Has", MSBuildValueKind.Bool }
	};

	static readonly Dictionary<string, MSBuildValueKind> knownSuffixes = new () {
		{ "Enabled", MSBuildValueKind.Bool },
		{ "Disabled", MSBuildValueKind.Bool },
		{ "Required", MSBuildValueKind.Bool },
		{ "Path", MSBuildValueKind.FileOrFolder },
		{ "Paths", MSBuildValueKind.FileOrFolder.AsList () },
		{ "Directory", MSBuildValueKind.Folder },
		{ "Dir", MSBuildValueKind.Folder },
		{ "File", MSBuildValueKind.File },
		{ "Filename", MSBuildValueKind.Filename },
		{ "Uri", MSBuildValueKind.Url },
		{ "Url", MSBuildValueKind.Url },
		{ "Ext", MSBuildValueKind.Extension },
		{ "Guid", MSBuildValueKind.Guid },
		{ "Directories", MSBuildValueKind.Folder.AsList () },
		{ "Dirs", MSBuildValueKind.Folder.AsList () },
		{ "Files", MSBuildValueKind.File.AsList () }
	};
}
