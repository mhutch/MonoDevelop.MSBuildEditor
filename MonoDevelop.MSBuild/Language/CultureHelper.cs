// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;

namespace MonoDevelop.MSBuild.Language;


// centralizing culture stuff here will allow eventually supporting intellisense
// for cultures that aren't available on the current system
static class CultureHelper
{
	static readonly Dictionary<string, KnownCulture> knownCultures;
	static readonly Lazy<Dictionary<int, KnownCulture>> knownCulturesByLcid = new(() => knownCultures.Values.Where (kc => kc.HasKnownLcid).ToDictionary (kc => kc.Lcid));

	static CultureHelper ()
	{
		knownCultures = new Dictionary<string, KnownCulture> (StringComparer.OrdinalIgnoreCase);
		foreach (var culture in CultureInfo.GetCultures (CultureTypes.AllCultures)) {
			if (TryCreateKnownCulture (culture, out var knownCulture)) {
				knownCultures.Add (knownCulture.Name, knownCulture);
			}
		}

		// GetCultures doesn't return Windows pseudo-localization cultures on all OS versions, so add them manually
		// https://github.com/dotnet/msbuild/pull/3654
		if (!knownCultures.ContainsKey ("qps-ploc")) {
			knownCultures.Add ("qps-ploc", new KnownCulture ("qps-ploc", "Pseudo Language (Pseudo)", 1281));
		}

		if (!knownCultures.ContainsKey ("qps-ploca")) {
			knownCultures.Add ("qps-ploca", new KnownCulture ("qps-ploca", "Pseudo Language (Pseudo Asia)", 1534));
		}

		if (!knownCultures.ContainsKey ("qps-plocm")) {
			knownCultures.Add ("qps-plocm", new KnownCulture ("qps-plocm", "Pseudo Language (Pseudo Mirrored)", 2559));
		}

		if (!knownCultures.ContainsKey ("qps-Latn-x-sh")) {
			knownCultures.Add ("qps-Latn-x-sh", new KnownCulture ("qps-Latn-x-sh", "Pseudo (Pseudo Selfhost)", 2305));
		}

	}
	static bool TryCreateKnownCulture (CultureInfo culture, out KnownCulture? knownCulture)
	{
		if (!string.IsNullOrEmpty (culture.Name) && !culture.EnglishName.StartsWith ("Unknown", StringComparison.Ordinal)) {
			knownCulture = new (culture.Name, culture.DisplayName, culture.LCID);
			return true;
		}
		knownCulture = null;
		return false;
	}

	static bool TryGetKnownCulture (string name, out KnownCulture knownCulture)
	{
		if (knownCultures.TryGetValue (name, out knownCulture)) {
			return true;
		}

		// Add this fallback just in case .NET or the OS returns a culture that isn't in the list of known cultures
		// as a result of aliasing or something.
		// Filter out cultures that are marked UserCustomCulture as they are created on demand for any valid BCP47 name.
		try {
			return (IsValidBcp47Name (name)
				&& CultureInfo.GetCultureInfo (name) is CultureInfo info
				&& !info.CultureTypes.HasFlag (CultureTypes.UserCustomCulture)
				&& TryCreateKnownCulture (info, out knownCulture));
		} catch (CultureNotFoundException) {
			return false;
		}
	}

	public static ICollection<KnownCulture> GetKnownCultures () => knownCultures.Values;

	public static bool IsValidCultureName (string cultureName) => IsValidBcp47Name (cultureName);

	public static bool IsKnownCulture (string cultureName) => TryGetKnownCulture (cultureName, out _);

	public static bool IsValidLcid (string value, out int lcid) => int.TryParse (value, out lcid) && lcid > 0;

	public static bool IsKnownLcid (int lcid) => knownCulturesByLcid.Value.ContainsKey (lcid);

	public static bool TryGetLcidSymbol (string lcidString, [NotNullWhen (true)] out ITypedSymbol? lcidSymbol)
	{
		lcidSymbol = null;
		return int.TryParse (lcidString, out int lcid) && TryGetLcidSymbol (lcid, out lcidSymbol);
	}

	public static bool TryGetLcidSymbol (int lcid, [NotNullWhen (true)] out ITypedSymbol? lcidSymbol)
	{
		if (knownCulturesByLcid.Value.TryGetValue (lcid, out KnownCulture culture)) {
			lcidSymbol = culture.CreateLcidSymbol ();
			return true;
		}
		lcidSymbol = null;
		return false;
	}

	public static bool TryGetCultureSymbol (string cultureName, [NotNullWhen (true)] out ITypedSymbol? cultureSymbol)
	{
		if (TryGetKnownCulture (cultureName, out KnownCulture culture)) {
			cultureSymbol = culture.CreateCultureSymbol ();
			return true;
		}
		cultureSymbol = null;
		return false;
	}

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
