using System.Threading.Tasks;
using MonoDevelop.Ide;
using NUnit.Framework;
using MonoDevelop.Ide.CodeCompletion;

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
			AssertContainsItem (provider, "PropertyGroup");
			AssertContainsItem (provider, "Choose");
			Assert.AreEqual (12, provider.Count);
		}

		[Test]
		public async Task InferredItems ()
		{
			var provider = await MSBuildEditorTesting.CreateProvider (@"
<Project><ItemGroup><Foo /><Bar /><$", ".csproj");
			Assert.IsNotNull (provider);
			AssertContainsItem (provider, "Foo");
			AssertContainsItem (provider, "Bar");
			// comment, cdata, closing tags for Project and ItemGroup, plus the actual two items
			Assert.AreEqual (6, provider.Count);
		}

		[Test]
		public async Task InferredMetadata ()
		{
			var provider = await MSBuildEditorTesting.CreateProvider (@"
<Project><ItemGroup><Foo><Bar>a</Bar></Foo><Foo><$", ".csproj");
			Assert.IsNotNull (provider);
			AssertContainsItem (provider, "Bar");
			Assert.AreEqual (6, provider.Count);
		}

		[Test]
		public async Task InferredMetadataAttribute ()
		{
			var provider = await MSBuildEditorTesting.CreateProvider (@"
<Project><ItemGroup><Foo Bar=""a"" /><Foo $", ".csproj", true);
			Assert.IsNotNull (provider);
			AssertContainsItem (provider, "Bar");
			AssertContainsItem (provider, "Include");
			Assert.AreEqual (7, provider.Count);
		}

		[Test]
		public async Task ProjectConfigurationConfigInference ()
		{
			var provider = await MSBuildEditorTesting.CreateProvider (@"
<Project><ItemGroup>
<ProjectConfiguration Configuration='Foo' Platform='Bar' Include='Foo|Bar' />
<Baz Condition=""$(Configuration)=='^", ".csproj", true, '^');
			Assert.IsNotNull (provider);
			AssertContainsItem (provider, "Foo");
			Assert.AreEqual (3, provider.Count);
		}

		[Test]
		public async Task ProjectConfigurationPlatformInference ()
		{
			var provider = await MSBuildEditorTesting.CreateProvider (@"
<Project><ItemGroup>
<ProjectConfiguration Configuration='Foo' Platform='Bar' Include='Foo|Bar' />
<Baz Condition=""$(Platform)=='^", ".csproj", true, '^');
			Assert.IsNotNull (provider);
			AssertContainsItem (provider, "Bar");
			Assert.AreEqual (3, provider.Count);
		}

		[Test]
		public async Task ConfigurationsInference ()
		{
			var provider = await MSBuildEditorTesting.CreateProvider (@"
<Project>
<PropertyGroup><Configurations>Foo;Bar</Configurations></PropertyGroup>
<ItemGroup>
<Baz Condition=""$(Configuration)=='^", ".csproj", true, '^');
			Assert.IsNotNull (provider);
			AssertContainsItem (provider, "Foo");
			AssertContainsItem (provider, "Bar");
			Assert.AreEqual (4, provider.Count);
		}

		[Test]
		public async Task PlatformsInference ()
		{
			var provider = await MSBuildEditorTesting.CreateProvider (@"
<Project>
<PropertyGroup><Platforms>Foo;Bar</Platforms></PropertyGroup>
<ItemGroup>
<Baz Condition=""$(Platform)=='^", ".csproj", true, '^');
			Assert.IsNotNull (provider);
			AssertContainsItem (provider, "Foo");
			AssertContainsItem (provider, "Bar");
			Assert.AreEqual (4, provider.Count);
		}

		[Test]
		public async Task ConditionConfigurationInference ()
		{
			var provider = await MSBuildEditorTesting.CreateProvider (@"
<Project>
<PropertyGroup Condition=""$(Configuration)=='Foo'"" />
<ItemGroup>
<Baz Condition=""$(Configuration)=='^", ".csproj", true, '^');
			Assert.IsNotNull (provider);
			AssertContainsItem (provider, "Foo");
			Assert.AreEqual (3, provider.Count);
		}

		[Test]
		public async Task PlatformConfigurationInference ()
		{
			var provider = await MSBuildEditorTesting.CreateProvider (@"
<Project>
<PropertyGroup Condition=""$(Platform)=='Foo'"" />
<ItemGroup>
<Baz Condition=""$(Platform)=='^", ".csproj", true, '^');
			Assert.IsNotNull (provider);
			AssertContainsItem (provider, "Foo");
			Assert.AreEqual (3, provider.Count);
		}

		[Test]
		public async Task ConfigurationAndPlatformInference ()
		{
			var provider = await MSBuildEditorTesting.CreateProvider (@"
<Project>
<PropertyGroup Condition=""'$(Platform)|$(Configuration)'=='Foo|Bar'"" />
<ItemGroup>
<Baz Condition=""'$(Platform)|$(Configuration)'=='^", ".csproj", true, '^');
			Assert.IsNotNull (provider);
			AssertContainsItem (provider, "Foo");
			AssertContainsItem (provider, "Bar");
			Assert.AreEqual (4, provider.Count);
		}

		static void AssertContainsItem (CompletionDataList list, string displayName)
		{
			foreach (var data in list) {
				if (data.DisplayText == displayName) {
					return;
				}
			}
			Assert.Fail ($"List does not contain expected item {displayName}");
		}

		static void AssertDoesNotContainItem (CompletionDataList list, string displayName)
		{
			foreach (var data in list) {
				if (data.DisplayText == displayName) {
					Assert.Fail ($"List contains unexpected item {displayName}");
				}
			}
		}

		[Test]
		public async Task IntrinsicStaticPropertyFunctionCompletion ()
		{
			var provider = await MSBuildEditorTesting.CreateProvider (@"
<Project>
<PropertyGroup>
<Foo>$([MSBuild]::^", ".csproj", true, '^');
			Assert.IsNotNull (provider);
			AssertContainsItem (provider, "GetDirectoryNameOfFileAbove");
			Assert.AreEqual (32, provider.Count);
		}

		[Test]
		public async Task StaticPropertyFunctionCompletion ()
		{
			var provider = await MSBuildEditorTesting.CreateProvider (@"
<Project>
<PropertyGroup>
<Foo>$([System.String]::^", ".csproj", true, '^');
			Assert.IsNotNull (provider);
			AssertContainsItem (provider, "new");
			AssertContainsItem (provider, "Join");
			AssertDoesNotContainItem (provider, "ToLower");
		}

		[Test]
		public async Task PropertyStringFunctionCompletion ()
		{
			var provider = await MSBuildEditorTesting.CreateProvider (@"
<Project>
<PropertyGroup>
<Foo>$(Foo.^", ".csproj", true, '^');
			Assert.IsNotNull (provider);
			//string functions
			AssertContainsItem (provider, "ToLower");
			//properties can be accessed with the getter method
			AssertContainsItem (provider, "get_Length");
			//.net properties are allowed for properties
			AssertContainsItem (provider, "Length");
			//indexers should be filtered out
			AssertDoesNotContainItem (provider, "this[]");
		}

		[Test]
		public async Task ItemFunctionCompletion ()
		{
			var provider = await MSBuildEditorTesting.CreateProvider (@"
<Project>
<PropertyGroup>
<Foo>@(Foo->^", ".csproj", true, '^');
			Assert.IsNotNull (provider);
			//intrinsic functions
			AssertContainsItem (provider, "DistinctWithCase");
			AssertContainsItem (provider, "Metadata");
			//string functions
			AssertContainsItem (provider, "ToLower");
			//properties can be accessed with the getter method
			AssertContainsItem (provider, "get_Length");
			//.net properties are not allowed for items
			AssertDoesNotContainItem (provider, "Length");
			//indexers should be filtered out
			AssertDoesNotContainItem (provider, "this[]");
		}

		[Test]
		public async Task PropertyFunctionClassNames ()
		{
			var provider = await MSBuildEditorTesting.CreateProvider (@"
<Project>
<PropertyGroup>
<Foo>$([^", ".csproj", true, '^');
			Assert.IsNotNull (provider);
			AssertContainsItem (provider, "MSBuild");
			AssertContainsItem (provider, "System.String");
		}

		[Test]
		public async Task PropertyFunctionChaining ()
		{
			var provider = await MSBuildEditorTesting.CreateProvider (@"
<Project>
<PropertyGroup>
<Foo>$([System.DateTime]::Now.^", ".csproj", true, '^');
			Assert.IsNotNull (provider);
			AssertContainsItem (provider, "AddDays");
		}

		[Test]
		public async Task IndexerChaining ()
		{
			var provider = await MSBuildEditorTesting.CreateProvider (@"
<Project>
<PropertyGroup>
<Foo>$(Foo[0].^", ".csproj", true, '^');
			Assert.IsNotNull (provider);
			AssertContainsItem (provider, "CompareTo");
			AssertDoesNotContainItem (provider, "Substring");
		}
	}
}

