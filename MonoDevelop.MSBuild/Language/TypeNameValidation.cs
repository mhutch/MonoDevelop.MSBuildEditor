// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETCOREAPP
#nullable enable
#endif

using System.Globalization;

namespace MonoDevelop.MSBuild.Language;

static class TypeNameValidation
{
	public static bool IsValidCSharpTypeOrNamespace (string value) => IsValidCSharpTypeOrNamespace(value, out _, out _);

	public static bool IsValidCSharpTypeOrNamespace (string value, out int componentCount, out bool isGenericType)
	{
		int offset = 0;
		if (!ConsumeCSharpGenericTypeOrNamespace(value, ref offset, out componentCount, out isGenericType)) {
			return false;
		}
		return offset == value.Length;
	}


	static bool ConsumeCSharpGenericTypeOrNamespace (string value, ref int offset, out int componentCount, out bool isGenericType)
	{
		isGenericType = false;

		componentCount = ConsumeIdentifiers(value, ref offset);

		if (componentCount == 0) {
			return false;
		}

		if (offset == value.Length) {
			return true;
		}

		// try to consume generics in the form SomeType<SomeOtherType,AnotherType>
		if (value[offset] != '<') {
			// complete but not a generic type, we are done consuming, caller decides what to do with next char
			return true;
		}
		offset++;

		isGenericType = true;
		do {
			// Ignore whitespace after a comma as that's pretty common e.g. SomeType<SomeOtherType, AnotherType>
			// and is valid if this is going to be injected verbatim into a C# file.
			// We could be more strict and disallow whitespace, but that will likely trip people up.
			// We could also be more liberal and allow general whitespace around the angle brackets and commas,
			// but that's a bit of a rabbit hole and not likely to be very useful.
			if (value[offset] == ',') {
				offset++;
				ConsumeSpaces(value, ref offset);
			}
			if (!ConsumeCSharpGenericTypeOrNamespace(value, ref offset, out int consumedComponents, out _)) {
				return false;
			}
		} while (value[offset] == ',');

		if (offset < value.Length && value[offset] == '>') {
			offset++;
			return true;
		}

		return false;
	}

	static void ConsumeSpaces(string value, ref int offset)
	{
		while (value[offset] == ' ') {
			offset++;
		}
	}

	public static bool IsValidClrNamespace (string value) => IsValidClrTypeOrNamespace(value, out _, out var hasGenerics) && !hasGenerics;

	public static bool IsValidClrTypeName (string value) => IsValidClrTypeOrNamespace(value, out var componentCount, out var hasGenerics) && componentCount == 1 && !hasGenerics;

	public static bool IsValidClrTypeOrNamespace (string value) => IsValidClrTypeOrNamespace(value, out _, out _);

	public static bool IsValidClrTypeOrNamespace (string value, out int componentCount, out bool isGenericType)
	{
		int offset = 0;
		if (!ConsumeClrGenericTypeOrNamespace(value, ref offset, out componentCount, out isGenericType)) {
			return false;
		}
		return offset == value.Length;
	}

	static bool ConsumeClrGenericTypeOrNamespace (string value, ref int offset, out int componentCount, out bool isGenericType)
	{
		isGenericType = false;

		componentCount = ConsumeIdentifiers(value, ref offset);

		if (componentCount == 0) {
			return false;
		}

		if (offset == value.Length) {
			return true;
		}

		// try to consume generics in the form SomeType`2[SomeOtherType,AnotherType]
		if (value[offset] != '`') {
			// complete but not a generic type, we are done consuming, caller decides what to do with next char
			return true;
		}
		offset++;

		int argCountOffset = offset;
		if(!ConsumeNumbers(value, ref offset) || !int.TryParse(value.Substring(argCountOffset, offset - argCountOffset), out int genericArgCount)) {
			return false;
		}

		isGenericType = true;
		if (value[offset++] != '[') {
			return false;
		}

		for (int i = 0; i < genericArgCount; i++) {
			// recursively consume the generic type arguments
			if (!ConsumeClrGenericTypeOrNamespace(value, ref offset, out int consumedComponents, out _)) {
				return false;
			}
			if (i < genericArgCount - 1) {
				if (offset < value.Length && value[offset] == ',') {
					offset++;
				} else {
					return false;
				}
			}
		}

		if (offset < value.Length && value[offset] == ']') {
			offset++;
			return true;
		}

		return false;
	}

	static bool ConsumeNumbers (string value, ref int offset)
	{
		int start = offset;
		while(char.IsDigit(value[offset])) {
			offset++;
		}
		return offset > start;
	}

	static int ConsumeIdentifiers (string value, ref int offset)
	{
		int componentCount = 0;

		while(ConsumeIdentifier(value, ref offset)) {
			componentCount++;
			if (offset >= value.Length || value[offset] != '.') {
				return componentCount;
			}
			offset++;
		}

		if (componentCount > 0 && value[offset - 1] == '.') {
			// we consumed a dot but no identifier followed it, so walk it back
			offset--;
		}

		return componentCount;
	}

	static bool ConsumeIdentifier (string value, ref int offset)
	{
		bool nextMustBeStartChar = true;

		while (offset < value.Length) {
			char ch = value[offset];
			// based on https://github.com/dotnet/runtime/blob/96be510135829ff199c6c341ad461c36bab4809e/src/libraries/Common/src/System/CSharpHelpers.cs#L141
			UnicodeCategory uc = CharUnicodeInfo.GetUnicodeCategory(ch);
			switch (uc) {
				case UnicodeCategory.UppercaseLetter:        // Lu
				case UnicodeCategory.LowercaseLetter:        // Ll
				case UnicodeCategory.TitlecaseLetter:        // Lt
				case UnicodeCategory.ModifierLetter:         // Lm
				case UnicodeCategory.LetterNumber:           // Lm
				case UnicodeCategory.OtherLetter:            // Lo
					nextMustBeStartChar = false;
					offset++;
					continue;

				case UnicodeCategory.NonSpacingMark:         // Mn
				case UnicodeCategory.SpacingCombiningMark:   // Mc
				case UnicodeCategory.ConnectorPunctuation:   // Pc
				case UnicodeCategory.DecimalDigitNumber:     // Nd
					// Underscore is a valid starting character, even though it is a ConnectorPunctuation.
					if (nextMustBeStartChar && ch != '_')
						return false;
					offset++;
					continue;

				default:
					// unlike CSharpHelpers.cs, don't check IsSpecialTypeChar here because we're only looking
					// for identifier components that are valid for all languages, and generic syntax is
					// handled separately.
					//
					break;
			}
			break;
		}

		// return true if we have been able to consume a valid identifier. it need
		// not be the entire string.
		return !nextMustBeStartChar;
	}

	public static bool IsValidCSharpType (string value, out int componentCount)
	{
		string[] components = value.Split ('.');
		componentCount = components.Length;
		foreach (var component in components) {
			if (!System.CodeDom.Compiler.CodeGenerator.IsValidLanguageIndependentIdentifier (component)) {
				return false;
			}
		}
		return true;
	}
}
