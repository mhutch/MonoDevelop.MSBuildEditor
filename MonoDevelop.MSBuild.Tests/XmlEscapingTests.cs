// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NUnit.Framework;
using MonoDevelop.MSBuild.Language;

namespace MonoDevelop.MSBuild.Tests
{
	[TestFixture]
	public class XmlEscapingTests
	{
		[Test]
		[TestCase ("abc &lt;&gt; &apos; def", "abc <> ' def")]
		[TestCase ( "&amp;&quot;", "&\"")]
		public void Unescape (string escaped, string unescaped)
		{
			Assert.AreEqual (
				unescaped,
				XmlEscaping.UnescapeEntities (escaped)
			);
		}
	}
}
