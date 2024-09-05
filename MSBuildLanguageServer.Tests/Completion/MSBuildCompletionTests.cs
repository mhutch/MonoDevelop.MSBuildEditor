// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


using Microsoft.CodeAnalysis.LanguageServer;
using Microsoft.CodeAnalysis.Test.Utilities;

using MonoDevelop.MSBuild.LanguageServer.Tests;
using MonoDevelop.Xml.Tests.Utils;

using Roslyn.Test.Utilities;

using Xunit.Abstractions;

using LSP = Roslyn.LanguageServer.Protocol;

namespace MonoDevelop.MSBuild.Tests.Editor.Completion;

public class MSBuildCompletionTests(ITestOutputHelper testOutputHelper) : AbstractLanguageServerProtocolTests(testOutputHelper)
{
    [Fact]
    public async Task ProjectElementCompletion()
    {
        var result = await this.GetCompletionContext("$");
        Assert.NotNull(result);
        result.AssertNonEmpty();

        result.AssertContains("<Project");
    }

    [Fact]
    public async Task ProjectElementBracketCompletion()
    {
        var result = await this.GetCompletionContext("<$>");

        result.AssertNonEmpty();

        result.AssertContains("<Project");
    }

    [Fact]
    public async Task ProjectChildElementCompletion()
    {
        var result = await this.GetCompletionContext("<Project>$");

        result.AssertNonEmpty();

        result.AssertContains("<ItemGroup");
        result.AssertContains("<Choose");
        result.AssertContains("<Import");
    }


    [Fact]
    public async Task ProjectChildElementBracketCompletion()
    {
        var result = await this.GetCompletionContext(@"<Project><$");

        result.AssertItemCount(12);

        result.AssertContains("<PropertyGroup");
        result.AssertContains("<Choose");
        result.AssertContains("</Project");
        result.AssertContains("<!--");
    }

    [Fact]
    public async Task TaskCompletion()
    {
        var result = await this.GetCompletionContext(@"<Project><Target><$");

        result.AssertContains("<Message");
        result.AssertContains("<Exec");
        result.AssertContains("<Csc");
    }

    [Fact]
    public async Task TaskParameterCompletion()
    {
        var result = await this.GetCompletionContext(@"<Project><Target><Message $");

        result.AssertContains("Importance");
        result.AssertContains("Text");
        result.AssertContains("HelpKeyword");
        result.AssertContains("Condition");
    }

    [Fact]
    public async Task MessageImportanceCompletion()
    {
        var result = await this.GetCompletionContext(@"<Project><Target><Message Importance=""$");

        result.AssertContains("High");
        result.AssertContains("Normal");
        result.AssertContains("Low");
    }

    [Fact]
    public async Task TaskOutputCompletion()
    {
        var result = await this.GetCompletionContext(@"<Project><Target><Csc><Output TaskParameter=""$");

        result.AssertContains("OutputAssembly");
        result.AssertContains("OutputRefAssembly");
    }

    [Fact]
    public async Task InferredItems()
    {
        var result = await this.GetCompletionContext(@"
<Project><ItemGroup><Foo /><Bar /><$");

        result.AssertContains("<Foo");
        result.AssertContains("<Bar");
        result.AssertContains("</ItemGroup");
        result.AssertContains("</Project");
        result.AssertContains("<!--");
    }

    [Fact]
    public async Task InferredMetadata()
    {
        var result = await this.GetCompletionContext(@"
<Project><ItemGroup><Foo><Bar>a</Bar></Foo><Foo><$");

        result.AssertItemCount(5);

        result.AssertContains("<Bar");
    }

    [Fact]
    public async Task InferredMetadataAttribute()
    {
        var result = await this.GetCompletionContext(@"
<Project><ItemGroup><Foo Bar=""a"" /><Foo $");

        result.AssertItemCount(7);

        result.AssertContains("Bar");
        result.AssertContains("Include");
    }

    [Fact]
    public async Task ProjectConfigurationConfigInference()
    {
        var result = await this.GetCompletionContext(@"
<Project><ItemGroup>
<ProjectConfiguration Configuration='Foo' Platform='Bar' Include='Foo|Bar' />
<Baz Condition=""$(Configuration)=='^", caretMarker: '^');

        result.AssertItemCount(5);

        result.AssertContains("Foo");
        result.AssertContains("Debug");
        result.AssertContains("Release");
        result.AssertContains("$(");
        result.AssertContains("@(");
    }

    [Fact]
    public async Task ProjectConfigurationPlatformInference()
    {
        var result = await this.GetCompletionContext(@"
<Project><ItemGroup>
<ProjectConfiguration Configuration='Foo' Platform='Bar' Include='Foo|Bar' />
<Baz Condition=""$(Platform)=='^", caretMarker: '^');

        result.AssertItemCount(4);

        result.AssertContains("Bar");
        result.AssertContains("AnyCPU");
        result.AssertContains("$(");
        result.AssertContains("@(");
    }

    [Fact]
    public async Task ConfigurationsInference()
    {
        var result = await this.GetCompletionContext(@"
<Project>
<PropertyGroup><Configurations>Foo;Bar</Configurations></PropertyGroup>
<ItemGroup>
<Baz Condition=""$(Configuration)=='^", caretMarker: '^');

        result.AssertItemCount(6);

        result.AssertContains("Foo");
        result.AssertContains("Bar");
        result.AssertContains("Debug");
        result.AssertContains("Release");
        result.AssertContains("$(");
        result.AssertContains("@(");
    }

    [Fact]
    public async Task PlatformsInference()
    {
        var result = await this.GetCompletionContext(@"
<Project>
<PropertyGroup><Platforms>Foo;Bar</Platforms></PropertyGroup>
<ItemGroup>
<Baz Condition=""$(Platform)=='^", caretMarker: '^');

        result.AssertItemCount(5);

        result.AssertContains("Foo");
        result.AssertContains("Bar");
        result.AssertContains("AnyCPU");
        result.AssertDoesNotContain("Exists");
        result.AssertDoesNotContain("HasTrailingSlash");
    }

    [Fact]
    public async Task ConditionConfigurationInference()
    {
        var result = await this.GetCompletionContext(@"
<Project>
<PropertyGroup Condition=""$(Configuration)=='Foo'"" />
<ItemGroup>
<Baz Condition=""$(Configuration)=='^", caretMarker: '^');

        result.AssertItemCount(5);

        result.AssertContains("Foo");
        result.AssertContains("Debug");
        result.AssertContains("Release");
        result.AssertContains("$(");
        result.AssertContains("@(");
        result.AssertDoesNotContain("Exists");
        result.AssertDoesNotContain("HasTrailingSlash");
    }

    [Fact]
    public async Task PlatformConfigurationInference()
    {
        var result = await this.GetCompletionContext(@"
<Project>
<PropertyGroup Condition=""$(Platform)=='Foo'"" />
<ItemGroup>
<Baz Condition=""$(Platform)=='^", caretMarker: '^');

        result.AssertItemCount(4);

        result.AssertContains("Foo");
        result.AssertContains("AnyCPU");
        result.AssertContains("$(");
        result.AssertContains("@(");
        result.AssertDoesNotContain("Exists");
        result.AssertDoesNotContain("HasTrailingSlash");
    }

    [Fact]
    public async Task ConfigurationAndPlatformInference()
    {
        var result = await this.GetCompletionContext(@"
<Project>
<PropertyGroup Condition=""'$(Platform)|$(Configuration)'=='Foo|Bar'"" />
<ItemGroup>
<Baz Condition=""'$(Platform)|$(Configuration)'=='^", caretMarker: '^');

        result.AssertItemCount(7);

        result.AssertContains("Foo");
        result.AssertContains("Bar");
        result.AssertContains("Debug");
        result.AssertContains("Release");
        result.AssertContains("AnyCPU");
        result.AssertContains("$(");
        result.AssertContains("@(");
        result.AssertDoesNotContain("Exists");
        result.AssertDoesNotContain("HasTrailingSlash");
    }

    [Fact]
    public async Task IntrinsicStaticPropertyFunctionCompletion()
    {
        var result = await this.GetCompletionContext(@"
<Project>
<PropertyGroup>
<Foo>$([MSBuild]::^", caretMarker: '^');

        // check a few different expected values are in the list
        result.AssertContains("GetDirectoryNameOfFileAbove");
        result.AssertContains("Add");
        result.AssertContains("GetTargetPlatformVersion");
    }

    [Fact]
    public async Task StaticPropertyFunctionCompletion()
    {
        var result = await this.GetCompletionContext(@"
<Project>
<PropertyGroup>
<Foo>$([System.String]::^", caretMarker: '^');

        result.AssertNonEmpty();

        result.AssertContains("new");
        result.AssertContains("Join");
        result.AssertDoesNotContain("ToLower");
    }

    [Fact]
    public async Task PropertyStringFunctionCompletion()
    {
        var result = await this.GetCompletionContext(@"
<Project>
<PropertyGroup>
<Foo>$(Foo.^", caretMarker: '^');

        result.AssertNonEmpty();

        //string functions
        result.AssertContains("ToLower");
        //properties can be accessed with the getter method
        result.AssertContains("get_Length");
        //.net properties are allowed for properties
        result.AssertContains("Length");
        //indexers should be filtered out
        result.AssertDoesNotContain("this[]");
        // ctors should be filtered out, cannot call on existing instance
        result.AssertDoesNotContain("new");
    }

    [Fact]
    public async Task PropertyFunctionArrayPropertyCompletion()
    {
        var result = await this.GetCompletionContext(@"
<Project>
<PropertyGroup>
<Foo>$([System.IO.Directory]::GetDirectories('.').^", caretMarker: '^');

        result.AssertNonEmpty();
        result.AssertContains("Length");
    }

    [Fact]
    public async Task PropertyFunctionArrayIndexerCompletion()
    {
        var result = await this.GetCompletionContext(@"
<Project>
<PropertyGroup>
<Foo>$([System.IO.Directory]::GetDirectories('.')[0].^", caretMarker: '^');

        result.AssertNonEmpty();
        result.AssertContains("get_Chars");
    }

    [Fact]
    public async Task ItemFunctionCompletion()
    {
        var result = await this.GetCompletionContext(@"
<Project>
<PropertyGroup>
<Foo>@(Foo->^", caretMarker: '^');

        result.AssertNonEmpty();

        //intrinsic functions
        result.AssertContains("DistinctWithCase");
        result.AssertContains("Metadata");
        //string functions
        result.AssertContains("ToLower");
        //properties can be accessed with the getter method
        result.AssertContains("get_Length");
        //.net properties are not allowed for items
        result.AssertDoesNotContain("Length");
        //indexers should be filtered out
        result.AssertDoesNotContain("this[]");
    }

    [Fact]
    public async Task PropertyFunctionClassNames()
    {
        var result = await this.GetCompletionContext(@"
<Project>
<PropertyGroup>
<Foo>$([^", caretMarker: '^');

        result.AssertNonEmpty();
        result.AssertContains("MSBuild");
        result.AssertContains("System.String");
    }

    [Fact]
    public async Task PropertyFunctionChaining()
    {
        var result = await this.GetCompletionContext(@"
<Project>
<PropertyGroup>
<Foo>$([System.DateTime]::Now.^", caretMarker: '^');

        result.AssertNonEmpty();
        result.AssertContains("AddDays");
    }

    [Fact]
    public async Task IndexerChaining()
    {
        var result = await this.GetCompletionContext(@"
<Project>
<PropertyGroup>
<Foo>$(Foo[0].^", caretMarker: '^');

        result.AssertNonEmpty();
        result.AssertContains("CompareTo");
        result.AssertDoesNotContain("Substring");
    }

    [Fact]
    public async Task EagerAttributeTrigger()
    {
        var result = await this.GetCompletionContext(@"<Project ToolsVersion=""$");

        result.AssertNonEmpty();
        result.AssertContains("4.0");
    }

    [Fact]
    public async Task EagerElementTrigger()
    {
        var result = await this.GetCompletionContext(
            @"<Project><PropertyGroup><Foo>$",
            filename: "EagerElementTrigger.csproj",
            composition: EditorFeaturesLspComposition.AddParts(typeof(TestSchemaProvider))
        );

        result.AssertNonEmpty();
        result.AssertContains("True");
    }

    // LSP doesn't support trigger on backspace, disable these for now
    /*
    [Fact]
    public async Task TriggerOnBackspace()
    {
        var result = await this.GetCompletionContext(
            @"<Project><PropertyGroup><Foo>$",
            CompletionTriggerReason.Backspace,
            filename: "EagerElementTrigger.csproj");

        result.AssertNonEmpty();
        result.AssertContains("True");
    }

    [Fact]
    public async Task NoTriggerOnBackspaceMidExpression()
    {
        var result = await this.GetCompletionContext(
            @"<Project><PropertyGroup><Foo>true$",
            CompletionTriggerReason.Backspace,
            filename: "EagerElementTrigger.csproj");

        Assert.Zero(result.ItemList.Count);
    }
    */

    [Fact]
    public async Task TriggerOnFirstNameChar()
    {
        var result = await this.GetCompletionContext(
            @"<Project><PropertyGroup><F$",
            filename: "EagerElementTrigger.csproj",
            composition: EditorFeaturesLspComposition.AddParts(typeof(TestSchemaProvider))
        );

        result.AssertNonEmpty();
        result.AssertContains("Foo");
    }

    [Fact]
    public async Task PathCompletion()
    {
        var testDirectory = TestMSBuildFileSystem.Instance.AddTestDirectory();
        testDirectory.AddFiles("foo.txt", "bar.cs", "baz.cs");
        testDirectory.AddDirectory("foo").AddFiles("hello.cs", "bye.cs");

        var result = await this.GetCompletionContext(
            @"<Project><PropertyGroup><FooPath>f$</FooPath>",
            filename: testDirectory.Combine("PathCompletion.csproj"),
            composition: EditorFeaturesLspComposition.AddParts(typeof(TestMSBuildFileSystemExport))
        );

        result.AssertNonEmpty();
        result.AssertContains("foo.txt");
    }

    [Fact]
    public async Task PathCompletionDirectory()
    {
        var testDirectory = TestMSBuildFileSystem.Instance.AddTestDirectory();
        testDirectory.AddFiles("foo.txt", "bar.cs", "baz.cs");
        testDirectory.AddDirectory("foo").AddFiles("hello.cs", "bye.cs");

        var result = await this.GetCompletionContext(
            @"<Project><PropertyGroup><FooPath>foo\$</FooPath>",
            filename: testDirectory.Combine("PathCompletion.csproj"),
            composition: EditorFeaturesLspComposition.AddParts(typeof(TestMSBuildFileSystemExport))
        );

        result.AssertNonEmpty();
        result.AssertContains("hello.cs");
    }

    async Task<LSP.CompletionList?> GetCompletionContext(
        string documentText,
        LSP.CompletionTriggerKind? triggerKind = null,
        char? triggerChar = null,
        char caretMarker = '$',
        string? filename = default,
        TestComposition? composition = null,
        CancellationToken cancellationToken = default)
    {
        (documentText, var caret) = TextWithMarkers.ExtractSingleLineColPosition(documentText, caretMarker);
        var caretPos = new LSP.Position { Line = caret.Line, Character = caret.Column };

        var capabilities = new LSP.ClientCapabilities {
            TextDocument = new LSP.TextDocumentClientCapabilities {
                Completion = new LSP.CompletionSetting()
            }
        };

        InitializationOptions initializationOptions = new() {
            ClientCapabilities = capabilities,
            ClientMessageFormatter = RoslynLanguageServer.CreateJsonMessageFormatter(excludeVSExtensionConverters: true)
        };

        await using var testLspServer = await CreateTestLspServerAsync(documentText, false, initializationOptions, composition);

        Uri documentUri;
        if (filename is not null)
        {
            documentUri = ProtocolConversions.CreateAbsoluteUri(Path.GetFullPath(filename));
        }
        else
        {
            documentUri = new Uri("file://foo.csproj");
        }

        await testLspServer.OpenDocument(documentUri, documentText, cancellationToken);

        return await testLspServer.GetCompletionList(documentUri, caretPos, triggerKind, triggerChar, cancellationToken);
    }
}