// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using System.Threading.Tasks;

using MonoDevelop.Xml.Editor.Tests.Extensions;

using NUnit.Framework;

namespace MonoDevelop.MSBuild.Tests
{
	[TestFixture]
	public class MSBuildQuickInfoTests : MSBuildEditorTest
	{
		[Test]
		public async Task TestItemGroupQuickInfo ()
		{
			var result = await this.GetQuickInfoItems ("<Project><Item$Group>");
			Assert.NotNull (result);
			Assert.IsTrue (result.Items.Any ());
		}

		[Test]
		public async Task TestClosingTag ()
		{
			var result = await this.GetQuickInfoItems ("<Project><PropertyGroup><Foo></Fo$o>");
			Assert.NotNull (result);
			Assert.IsTrue (result.Items.Any ());
		}
	}
}
