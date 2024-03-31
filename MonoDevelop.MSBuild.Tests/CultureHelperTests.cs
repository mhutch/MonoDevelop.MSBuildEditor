// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;

using MonoDevelop.MSBuild.Language;
using NUnit.Framework;

namespace MonoDevelop.MSBuild.Tests.Language
{
	[TestFixture]
	class CultureHelperTests
	{
		[TestCase ("en-US")]
		[TestCase ("EN-gb")]
		[TestCase ("pt-PT")]
		[TestCase ("qps-ploc")]
		public void ValidCultures (string cultureName)
		{
			var jp = CultureInfo.GetCultureInfo ("jp");
			Assert.True (CultureHelper.IsKnownCulture (cultureName));
			Assert.True (CultureHelper.TryGetCultureSymbol (cultureName, out var cultureSymbol));
			Assert.That (cultureName, Is.EqualTo (cultureSymbol.Name).IgnoreCase);
		}

		[TestCase ("")]
		[TestCase ("x3")]
		[TestCase ("55")]
		[TestCase ("1234")]
		[TestCase ("pineapple")]
		public void InvalidCulture (string cultureName)
		{
			Assert.False (CultureHelper.IsValidCultureName (cultureName));
			Assert.False (CultureHelper.IsKnownCulture (cultureName));
			Assert.False (CultureHelper.TryGetCultureSymbol (cultureName, out _));
		}

		[TestCase ("zz")]
		[TestCase ("jp")] // japan is ja, not jp
		[TestCase ("zz-ZZ")]
		[TestCase ("x-goat")]
		[TestCase ("ff-goat-EN")]
		[TestCase ("yyy")]
		public void UnknownCulture (string cultureName)
		{
			Assert.True (CultureHelper.IsValidCultureName (cultureName));
			Assert.False (CultureHelper.IsKnownCulture (cultureName));
			Assert.False (CultureHelper.TryGetCultureSymbol (cultureName, out _));
		}
	}
}