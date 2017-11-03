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
			Assert.AreEqual (10, provider.Count);
		}

		[Test]
		public async Task InferredMetadataAttribute ()
		{
			var provider = await MSBuildEditorTesting.CreateProvider (@"
<Project><ItemGroup><Foo Bar=""a"" /><Foo $", ".csproj", true);
			Assert.IsNotNull (provider);
			Assert.IsNotNull (provider.Find ("Bar=\"|\""));
			Assert.IsNotNull (provider.Find ("Include=\"|\""));
			Assert.AreEqual (6, provider.Count);
		}
	}
}

