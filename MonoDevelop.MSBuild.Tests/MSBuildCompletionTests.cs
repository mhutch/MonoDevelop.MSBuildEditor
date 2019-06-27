// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;

using Microsoft.VisualStudio.MiniEditor;

using MonoDevelop.MSBuild.Editor;
using MonoDevelop.Xml.Tests.Completion;
using MonoDevelop.Xml.Tests.EditorTestHelpers;

using NUnit.Framework;

namespace MonoDevelop.MSBuild.Tests
{
	[TestFixture]
	public class MSBuildCompletionTests : CompletionTestBase
	{
		[OneTimeSetUp]
		public void LoadMSBuild () => MSBuildTestHelpers.RegisterMSBuildAssemblies ();

		protected override string ContentTypeName => MSBuildContentType.Name;

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
}
