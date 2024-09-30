// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.MSBuild.Language;

using NUnit.Framework;

namespace MonoDevelop.MSBuild.Tests;

[TestFixture]
class TypeNameValidationTests
{
	[TestCase ("Foo", true, 1)]
	[TestCase ("Foo.Bar", true, 2)]
	[TestCase ("Foo.Bar`2[A,B]", true, 2, true)]
	[TestCase ("Foo.Bar`1[A]", true, 2, true)]
	[TestCase ("Bar`1[A]", true, 1, true)]
	[TestCase ("Bar`1[A`2[hi.hello,bye]]", true, 1, true)]
	[TestCase ("Bar`5[A]", false, 1, true)]
	[TestCase ("Bar`5[[A]", false, 1, true)]
	[TestCase ("Bar.", false, 1)]
	[TestCase ("Bar<A>", false, 1)]
	[TestCase ("_Bar_Baz1._Bar", true, 2)]
	[TestCase ("1Bar", false, 0)]
	[TestCase ("Bar-Baz", false, 1)]
	public void TestValidClrIdentifiers (string identifier, bool isValid, int componentCount, bool isGenericType = false)
	{
		bool actual = TypeNameValidation.IsValidClrTypeOrNamespace (identifier, out int actualComponentCount, out bool actualIsGenericType);
		Assert.AreEqual (isValid, actual);
		Assert.AreEqual (isGenericType, actualIsGenericType);
		Assert.AreEqual (componentCount, actualComponentCount);
	}

	[TestCase ("Foo", true, 1)]
	[TestCase ("Foo ", false, 1)]
	[TestCase ("Foo.Bar", true, 2)]
	[TestCase ("Foo.Bar<A,B>", true, 2, true)]
	[TestCase ("Foo.Bar<A>", true, 2, true)]
	[TestCase ("Bar<A>", true, 1, true)]
	[TestCase ("Bar<A<hi.hello,bye>>", true, 1, true)]
	[TestCase ("Bar<A<hi.hello, bye>>", true, 1, true)]
	[TestCase ("Bar<<A>", false, 1, true)]
	[TestCase ("Bar.", false, 1)]
	[TestCase ("Bar[A]", false, 1)]
	[TestCase ("Bar`1[A]", false, 1)]
	[TestCase ("_Bar_Baz1._Bar", true, 2)]
	[TestCase ("1Bar", false, 0)]
	[TestCase ("Bar-Baz", false, 1)]
	public void TestValidCSharpIdentifiers (string identifier, bool isValid, int componentCount, bool isGenericType = false)
	{
		bool actual = TypeNameValidation.IsValidCSharpTypeOrNamespace (identifier, out int actualComponentCount, out bool actualIsGenericType);
		Assert.AreEqual (isValid, actual);
		Assert.AreEqual (isGenericType, actualIsGenericType);
		Assert.AreEqual (componentCount, actualComponentCount);
	}
}