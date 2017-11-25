using System.Threading.Tasks;
using MonoDevelop.Ide;
using NUnit.Framework;

namespace MonoDevelop.MSBuildEditor.Tests
{
	[TestFixture]
	class MSBuildCompletionTests : IdeTestBase
	{
		[Test]
		public async Task ProjectCompletion ()
		{
			var provider = await MSBuildEditorTesting.CreateProvider (@"<Project><$", ".csproj");
			Assert.IsNotNull (provider);
			Assert.IsNotNull (provider.Find ("PropertyGroup"));
			Assert.IsNotNull (provider.Find ("Choose"));
			Assert.AreEqual (12, provider.Count);
		}

		[Test]
		public async Task InferredItems ()
		{
			var provider = await MSBuildEditorTesting.CreateProvider (@"
<Project><ItemGroup><Foo /><Bar /><$", ".csproj");
			Assert.IsNotNull (provider);
			Assert.IsNotNull (provider.Find ("Foo"));
			Assert.IsNotNull (provider.Find ("Bar"));
			// comment, cdata, closing tags for Project and ItemGroup, plus the actual two items
			Assert.AreEqual (6, provider.Count);
		}

		[Test]
		public async Task InferredMetadata ()
		{
			var provider = await MSBuildEditorTesting.CreateProvider (@"
<Project><ItemGroup><Foo><Bar>a</Bar></Foo><Foo><$", ".csproj");
			Assert.IsNotNull (provider);
			Assert.IsNotNull (provider.Find ("Bar"));
			Assert.AreEqual (6, provider.Count);
		}

		[Test]
		public async Task InferredMetadataAttribute ()
		{
			var provider = await MSBuildEditorTesting.CreateProvider (@"
<Project><ItemGroup><Foo Bar=""a"" /><Foo $", ".csproj", true);
			Assert.IsNotNull (provider);
			Assert.IsNotNull (provider.Find ("Bar"));
			Assert.IsNotNull (provider.Find ("Include"));
			Assert.AreEqual (10, provider.Count);
		}
	}
}

