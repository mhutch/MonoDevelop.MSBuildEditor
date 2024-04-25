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

	public static bool IsValid (string identifier) => TryGetLength (identifier, 0, identifier.Length - 1, out int length) && length == identifier.Length;

	/// <summary>
	/// Try to infer the value kind from an identifier.
	/// </summary>
	public static MSBuildValueKind InferValueKind (string name, MSBuildSyntaxKind syntaxKind)
	{
		if (syntaxKind == MSBuildSyntaxKind.Property || syntaxKind == MSBuildSyntaxKind.Metadata) {
			if (StartsWith ("Enable")
				|| StartsWith ("Disable")
				|| StartsWith ("Require")
				|| StartsWith ("Use")
				|| StartsWith ("Allow")
				|| EndsWith ("Enabled")
				|| EndsWith ("Disabled")
				|| EndsWith ("Required")) {
				return MSBuildValueKind.Bool;
			}
			if (EndsWith ("DependsOn")) {
				return MSBuildValueKind.TargetName.AsList ();
			}
			if (EndsWith ("Path")) {
				return MSBuildValueKind.FileOrFolder;
			}
			if (EndsWith ("Paths")) {
				return MSBuildValueKind.FileOrFolder.AsList ();
			}
			if (EndsWith ("Directory")
				|| EndsWith ("Dir")) {
				return MSBuildValueKind.Folder;
			}
			if (EndsWith ("File")) {
				return MSBuildValueKind.File;
			}
			if (EndsWith ("FileName")) {
				return MSBuildValueKind.Filename;
			}
			if (EndsWith ("Url")) {
				return MSBuildValueKind.Url;
			}
			if (EndsWith ("Ext")) {
				return MSBuildValueKind.Extension;
			}
			if (EndsWith ("Guid")) {
				return MSBuildValueKind.Guid;
			}
			if (EndsWith ("Directories") || EndsWith ("Dirs")) {
				return MSBuildValueKind.Folder.AsList ();
			}
			if (EndsWith ("Files")) {
				return MSBuildValueKind.File.AsList ();
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

		return MSBuildValueKind.Unknown;

		bool StartsWith (string prefix) => name.StartsWith (prefix, StringComparison.OrdinalIgnoreCase)
			&& name.Length > prefix.Length
			&& char.IsUpper (name[prefix.Length]);
		bool EndsWith (string suffix) => name.EndsWith (suffix, StringComparison.OrdinalIgnoreCase);
		bool Equals (string value) => name.Equals (value, StringComparison.OrdinalIgnoreCase);
	}
}
