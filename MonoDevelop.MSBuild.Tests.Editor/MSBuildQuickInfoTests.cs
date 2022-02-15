// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using System.Threading.Tasks;

using Microsoft.VisualStudio.MiniEditor;

using MonoDevelop.MSBuild.Editor;
using MonoDevelop.Xml.Tests.Completion;
using MonoDevelop.Xml.Tests.EditorTestHelpers;

using NUnit.Framework;

namespace MonoDevelop.MSBuild.Tests
{
	[TestFixture]
	public class MSBuildQuickInfoTests : CompletionTestBase
	{
		[OneTimeSetUp]
		public void LoadMSBuild () => MSBuildTestHelpers.RegisterMSBuildAssemblies ();

		protected override string ContentTypeName => MSBuildContentType.Name;

		[Test]
		public async Task TestItemGroupQuickInfo ()
		{
			var result = await GetQuickInfoItems ("<Project><Item$Group>");
			Assert.IsTrue (result.Items.Any ());
		}

		protected override (EditorEnvironment, EditorCatalog) InitializeEnvironment () => MSBuildTestEnvironment.EnsureInitialized ();
	}
}
