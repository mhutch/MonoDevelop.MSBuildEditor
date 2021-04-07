//
// Copyright (c) 2019 Microsoft Corp
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.IO;

using MonoDevelop.MSBuild.Language.Typesystem;
using MonoDevelop.MSBuild.Schema;
using NUnit.Framework;

namespace MonoDevelop.MSBuild.Tests
{
	[TestFixture]
	public class SchemaTests
	{
		MSBuildSchemaProvider provider = new MSBuildSchemaProvider ();

		[Test]
		[TestCase("microsoft.codeanalysis.targets", null)]
		[TestCase("microsoft.common.targets", null)]
		[TestCase("microsoft.visualbasic.currentversion.targets", null)]
		[TestCase("microsoft.csharp.currentversion.targets", null)]
		[TestCase("microsoft.cpp.targets", null)]
		[TestCase("nuget.build.tasks.pack.targets", null)]
		[TestCase("sdk.targets", "microsoft.net.sdk")]
		public void LoadCoreSchemas (string name, string sdk)
		{
			var schema = provider.GetSchema (name, sdk, out var loadErrors);
			Assert.NotNull (schema);
			Assert.Zero (loadErrors.Count);
		}

		[Test]
		public void CustomTypes ()
		{
			var schema = MSBuildSchema.Load (new StringReader (
@"{
	""properties"": {
		""MyProp"": {
			""type"": ""@mycustom""
		}
	},
	""customTypes"": {
		""mycustom"": {
			""$"": ""this is a comment"",
			""$name"": ""type-name"",
			""One"": ""x"",
			""Two"": ""y""
		}
	}
}"
			), out var loadErrors);

			Assert.Zero (loadErrors.Count);

			var item = new[] { schema }.GetProperty ("MyProp", true);
			Assert.NotNull (item);
			Assert.AreEqual (MSBuildValueKind.CustomType, item.ValueKind);
			Assert.NotNull (item.CustomType);
			Assert.AreEqual (2, item.CustomType.Values.Count);
			Assert.AreEqual ("type-name", item.CustomType.Name);
			Assert.AreEqual ("One", item.CustomType.Values[0].Name);
			Assert.AreEqual ("Two", item.CustomType.Values[1].Name);
			Assert.AreEqual ("x", item.CustomType.Values[0].Description.Text);
			Assert.AreEqual ("y", item.CustomType.Values[1].Description.Text);
		}
	}
}
