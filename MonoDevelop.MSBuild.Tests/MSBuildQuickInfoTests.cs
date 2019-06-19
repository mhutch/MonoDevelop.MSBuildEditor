// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.MiniEditor;
using Microsoft.VisualStudio.Utilities;
using MonoDevelop.MSBuild.Editor;
using MonoDevelop.Xml.Tests.Completion;
using MonoDevelop.Xml.Tests.EditorTestHelpers;
using NUnit.Framework;

namespace MonoDevelop.MSBuild.Tests
{
	[TestFixture]
	[Ignore ("There is currently no quickinfo broker")]
	public class MSBuildQuickInfoTests : CompletionTestBase
	{
		public override IContentType ContentType => Catalog.ContentTypeRegistryService.GetContentType (MSBuildContentType.Name);

		[Test]
		public async Task TestItemGroupQuickInfo ()
		{
			var result = await GetQuickInfoItems ("<Project><Item$Group>");
			Assert.IsTrue (result.Items.Any ());
		}

		protected override (EditorEnvironment, EditorCatalog) InitializeEnvironment () => TestEnvironment.EnsureInitialized ();
	}
}
