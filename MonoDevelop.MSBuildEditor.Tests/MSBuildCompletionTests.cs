using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MonoDevelop.MSBuildEditor.Tests
{
	[TestFixture]
	class WebFormsCompletionTests : UnitTests.TestBase
	{
		[Test]
		public async Task ProjectCompletion ()
		{
			var provider = await MSBuildEditorTesting.CreateProvider (@"<Project><$", ".aspx");
			Assert.IsNotNull (provider);
			Assert.AreEqual (9, provider.Count);
			Assert.IsNotNull (provider.Find ("Page"));
			Assert.IsNotNull (provider.Find ("Register"));
		}
	}
}

