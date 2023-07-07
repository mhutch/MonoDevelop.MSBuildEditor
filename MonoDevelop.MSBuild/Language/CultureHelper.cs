// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using MonoDevelop.MSBuild.Language.Typesystem;

namespace MonoDevelop.MSBuild.Language;

// centralizing culture stuff here will allow eventually supporting intellisense
// for cultures that aren't available on the current system
class CultureHelper
{
	public static bool IsValidCultureName (string cultureName) => IsValidBcp47Name (cultureName);

	public static bool IsKnownCulture (string cultureName)
	{
		try {
			var culture = CultureInfo.GetCultureInfo (cultureName);
			return IsKnownCulture (culture);
		} catch (CultureNotFoundException) {
		}
		return false;
	}

	public static bool IsValidLcid (string value, out int lcid) => int.TryParse (value, out lcid) && lcid > 0;

	public static bool IsKnownLcid (int lcid)
	{
		try {
			var culture = CultureInfo.GetCultureInfo (lcid);
			return HasKnownLcid (culture);
		} catch (CultureNotFoundException) {
		}
		return false;
	}

	public static bool TryGetLcidSymbol (string lcidString, [NotNullWhen (true)] out ISymbol? lcidSymbol)
	{
		lcidSymbol = null;
		return int.TryParse (lcidString, out int lcid) && TryGetLcidSymbol (lcid, out lcidSymbol);
	}

	// 4096 is the "not found" lcid
	static bool HasKnownLcid (CultureInfo culture) => culture.LCID != 4096;

	static bool IsKnownCulture (CultureInfo culture) => !culture.EnglishName.StartsWith ("Unknown", System.StringComparison.Ordinal);

	public static bool TryGetLcidSymbol (int lcid, [NotNullWhen (true)] out ISymbol? lcidSymbol)
	{
		try {
			var culture = CultureInfo.GetCultureInfo (lcid);
			if (HasKnownLcid (culture)) {
				lcidSymbol = CreateLcidSymbol (culture);
				return true;
			}
		} catch (CultureNotFoundException) {
		}
		lcidSymbol = null;
		return false;
	}

	public static bool TryGetCultureSymbol (string cultureName, [NotNullWhen (true)] out ISymbol? cultureSymbol)
	{
		try {
			var culture = CultureInfo.GetCultureInfo (cultureName);
			if (IsKnownCulture (culture)) {
				cultureSymbol = CreateCultureSymbol (culture);
				return true;
			}
		} catch (CultureNotFoundException) {
		}
		cultureSymbol = null;
		return false;
	}

	public static ISymbol CreateLcidSymbol (CultureInfo culture) => new ConstantSymbol (culture.LCID.ToString (), $"The LCID of the {culture.DisplayName} culture", MSBuildValueKind.Lcid);

	public static ISymbol CreateCultureSymbol (CultureInfo culture) => new ConstantSymbol (culture.Name, $"The name of the {culture.DisplayName} culture", MSBuildValueKind.Culture);

	internal static IEnumerable<CultureInfo> GetAllCultures () => CultureInfo.GetCultures (CultureTypes.AllCultures);

	// validate form of IETF BCP 47 language tag
	// TODO: this currently only validates the primary subtag and region subtag. anything else will cause an error.
	static bool IsValidBcp47Name (string name)
	{
		int index = 0;
		bool isValid;

		// primary language subtag: two letters, three letters, for 5-8 letters
		int primaryTagLength = TryConsumeBasicLatinLetters ();
		if (!(primaryTagLength == 2 || primaryTagLength == 3 || (primaryTagLength >= 5 && primaryTagLength <= 8))) {
			return false;
		}
		if (IsEndOrNonDash ()) {
			return isValid;
		}

		// skip: 0-3 extended language subtags, each 3 letters
		// skip: optional script subtag, 4 letters

		// optional region subtag, 2 letters, or 3 digits
		int regionSubtagLetters = TryConsumeBasicLatinLetters ();
		if (regionSubtagLetters == 2) {
			if (IsEndOrNonDash ()) {
				return isValid;
			}
		} else if (regionSubtagLetters != 0) {
			return false;
		}

		int regionSubtagNumbers = TryConsumeNumbers ();
		if (regionSubtagNumbers == 3) {
			if (IsEndOrNonDash ()) {
				return isValid;
			}
		} else if (regionSubtagLetters != 0) {
			return false;
		}

		return index >= name.Length;

		// skip: optional variant subtags, each 5-8 letters, or 4 characters starting with a digit
		// skip: optional extension subtags, each 1 letter (except x) followed by 1 or more subtags of 2-8 characters
		// skip: optional private-use subtag, x followed by 1 or more subtags of 1-8 characters

		static bool IsBasicLatinLetterChar (char c) => (c >= 'A' && c <= 'X') || (c >= 'a' && c <= 'z');
		static bool IsNumberChar (char c) => c >= '0' && c <= '9';

		int TryConsumeBasicLatinLetters ()
		{
			int consumed = 0;
			while (index < name.Length && IsBasicLatinLetterChar (name[index])) {
				consumed++;
				index++;
			}
			return consumed;
		}

		int TryConsumeNumbers ()
		{
			int consumed = 0;
			while (index < name.Length && IsNumberChar (name[index])) {
				consumed++;
				index++;
			}
			return consumed;
		}

		bool ConsumeDash ()
		{
			if (index < name.Length && name[index] == '-') {
				index++;
				return true;
			}
			return false;
		}

		bool IsEndOrNonDash ()
		{
			if (index == name.Length) {
				isValid = true;
				return true;
			}
			if (ConsumeDash ()) {
				isValid = true;
				return false;
			}
			isValid = false;
			return true;
		}
	}
}
