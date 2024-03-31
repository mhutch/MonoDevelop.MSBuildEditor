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
		if (!ReadSegment (out int segmentLength, out Bcp47SegmentChars segmentType)) {
			return false;
		}

		if (segmentLength == 1 && (FirstCharIs ('i') || FirstCharIs ('x'))) {
			// private or grandfathered, so we can't do much validation
			return true;
		}

		if (!(IsAlpha () && (IsLength (2) || IsLength (3) || IsLengthBetween (5, 8)))) {
			return false;
		}

		Bcp47Segment currentType = Bcp47Segment.PrimaryLanguage;

		while (ReadSegment (out segmentLength, out segmentType)) {
			switch (currentType) {
			case Bcp47Segment.PrimaryLanguage:
				if (IsExtendedLanguageSubtag ()) {
					currentType = Bcp47Segment.ExtendedLanguage1;
					continue;
				}
				goto case Bcp47Segment.ExtendedLanguage3;
			case Bcp47Segment.ExtendedLanguage1:
				if (IsExtendedLanguageSubtag ()) {
					currentType = Bcp47Segment.ExtendedLanguage2;
					continue;
				}
				goto case Bcp47Segment.ExtendedLanguage3;
			case Bcp47Segment.ExtendedLanguage2:
				if (IsExtendedLanguageSubtag ()) {
					currentType = Bcp47Segment.ExtendedLanguage3;
					continue;
				}
				goto case Bcp47Segment.ExtendedLanguage3;
			case Bcp47Segment.ExtendedLanguage3:
				if (IsLength (4) && IsAlpha ()) {
					currentType = Bcp47Segment.Script;
					continue;
				}
				goto case Bcp47Segment.Script;
			case Bcp47Segment.Script:
				if ((IsLength (2) && IsAlpha ()) || (IsLength (3) && IsNumeric ())) {
					currentType = Bcp47Segment.Region;
					continue;
				}
				goto case Bcp47Segment.Region;
			case Bcp47Segment.Region:
				goto case Bcp47Segment.Variant;
			case Bcp47Segment.Variant:
				if (IsVariant ()) {
					currentType = Bcp47Segment.Variant;
					continue;
				}
				goto case Bcp47Segment.ExtensionPrefix;
			case Bcp47Segment.ExtensionPrefix:
				if (IsLength (1)) {
					char first = FirstChar ();
					if (IsBasicLatinLetterChar (first) && first != 'x') {
						currentType = Bcp47Segment.Extension1;
						continue;
					}
					goto case Bcp47Segment.PrivateUsePrefix;
				}
				goto default;
			case Bcp47Segment.Extension1:
				if (IsLengthBetween (2, 8)) {
					currentType = Bcp47Segment.Extension2Plus;
					continue;
				}
				goto case default;
			case Bcp47Segment.Extension2Plus:
				if (IsLengthBetween (2, 8)) {
					currentType = Bcp47Segment.Extension2Plus;
					continue;
				}
				goto case Bcp47Segment.PrivateUsePrefix;
			case Bcp47Segment.PrivateUsePrefix:
				if (IsLength (1) && FirstCharIs ('x')) {
					currentType = Bcp47Segment.PrivateUse1;
					continue;
				}
				goto case default;
			case Bcp47Segment.PrivateUse1:
				if (IsLengthBetween (1, 8)) {
					currentType = Bcp47Segment.PrivateUse2Plus;
					continue;
				}
				goto case default;
			case Bcp47Segment.PrivateUse2Plus:
				if (IsLengthBetween (1, 8)) {
					currentType = Bcp47Segment.PrivateUse2Plus;
					continue;
				}
				goto case default;
			default:
				isValid = false;
				return false;
			}
		}

		if (currentType == Bcp47Segment.ExtensionPrefix || currentType == Bcp47Segment.PrivateUsePrefix) {
			isValid = false;
		}

		return isValid;

		bool IsAlpha () => segmentType == Bcp47SegmentChars.Letters;
		bool IsNumeric () => segmentType == Bcp47SegmentChars.Numbers;
		bool IsAlphaNumeric () => (segmentType & Bcp47SegmentChars.LettersAndNumbers) == segmentType;
		bool IsLength(int i) => segmentLength == i;
		bool IsLengthBetween (int i, int j) => segmentLength >=i && segmentLength <= j;
		bool IsExtendedLanguageSubtag () => IsLength (3) && IsAlpha ();
		bool IsVariant () => (IsLengthBetween (5, 8) && IsAlpha ()) || (IsLength (4) && IsAlphaNumeric () && IsNumberChar (FirstChar ()));
		char FirstChar () => name[index - segmentLength];
		bool FirstCharIs (char c) => name[index - segmentLength] == c;

		bool ReadSegment(out int segmentLength, out Bcp47SegmentChars type)
		{
			type = Bcp47SegmentChars.None;
			segmentLength = 0;

			if (index >= name.Length) {
				isValid = true;
				return false;
			}

			char ch = name[index];

			if (index == 0) {
				if (ch == '-') {
					isValid = false;
					return false;
				}
			} else if (ch == '-') {
				index++;
			} else {
				isValid = false;
				return false;
			}

			while (index < name.Length) {
				ch = name[index];
				if (IsBasicLatinLetterChar (ch)) {
					segmentLength++;
					index++;
					type |= Bcp47SegmentChars.Letters;
				} else if (IsNumberChar (ch)) {
					segmentLength++;
					index++;
					type |= Bcp47SegmentChars.Numbers;
				} else if (ch == '-') {
					break;
				} else {
					isValid = false;
					return true;
				}
			}

			isValid = segmentLength > 0;
			return isValid;
		}

		static bool IsBasicLatinLetterChar (char c) => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
		static bool IsNumberChar (char c) => c >= '0' && c <= '9';
	}

	enum Bcp47Segment
	{
		PrimaryLanguage,
		ExtendedLanguage1,
		ExtendedLanguage2,
		ExtendedLanguage3,
		Script,
		Region,
		Variant,
		ExtensionPrefix,
		Extension1,
		Extension2Plus,
		PrivateUsePrefix,
		PrivateUse1,
		PrivateUse2Plus
	}

	enum Bcp47SegmentChars
	{
		None = 0,
		Letters = 1,
		Numbers = 2,
		LettersAndNumbers = 3
	}
}
