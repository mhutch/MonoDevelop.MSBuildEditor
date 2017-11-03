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
	}
}

