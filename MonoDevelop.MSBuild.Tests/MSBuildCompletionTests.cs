// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.MiniEditor;
using Microsoft.VisualStudio.Utilities;
using MonoDevelop.MSBuild.Editor;
using MonoDevelop.Xml.Tests.Completion;
using MonoDevelop.Xml.Tests.EditorTestHelpers;
using NUnit.Framework;

namespace MonoDevelop.MSBuild.Tests
{
	[TestFixture]
	public class MSBuildCompletionTests : CompletionTestBase
	{
		public override IContentType ContentType => Catalog.ContentTypeRegistryService.GetContentType (MSBuildContentType.Name);

		[Test]
		public async Task TestElementCompletion ()
		{
			var result = await GetCompletionContext ("<Project>$");
			Assert.IsTrue (result.Items.Length > 0);
			result.AssertContains ("ItemGroup");
			result.AssertContains ("Choose");
			result.AssertContains ("Import");
		}

		protected override (EditorEnvironment, EditorCatalog) InitializeEnvironment () => TestEnvironment.EnsureInitialized ();
	}

	public static class CompletionTestExtensions
	{
		public static void AssertContains (this CompletionContext context, string name)
		{
			var item = context.Items.FirstOrDefault (i => i.DisplayText == name);
			Assert.NotNull (item, "Completion result is missing item '{0}'", name);
		}

		public static void AssertNonEmpty (this CompletionContext context) => Assert.NotZero (context.Items.Length);
	}
}
