// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;

using MonoDevelop.Xml.Editor.Tests.Extensions;

using NUnit.Framework;

namespace MonoDevelop.MSBuild.Tests.Editor.Completion
{
	[TestFixture]
	public class MSBuildCommitTests : MSBuildEditorTest
	{
		async Task TestTypeCommands (string filename, string before, string typeChars, string after)
		{
			//CommandServiceExtensions.EnableDebugTrace = true;
			//MSBuildCompletionSource.EnableDebugTrace = true;
			//try {
				await this.TestCommands (
					before,
					after,
					EditorAction.Type (typeChars),
					filename: filename,
					initialize: async (tv) => {
						tv.Options.SetOptionValue ("BraceCompletion/Enabled", true);
						// ensure we have an initial parse before triggering completion
						await Catalog.MSBuildParserProvider.GetParser (tv.TextBuffer).GetOrProcessAsync (tv.TextBuffer.CurrentSnapshot, CancellationToken.None);
					}
				);
			//} finally {
			//	CommandServiceExtensions.EnableDebugTrace = false;
			//	MSBuildCompletionSource.EnableDebugTrace = false;
			//}
		}

		[Test]
		public Task CommitPropertyValue ()
			=> TestTypeCommands (
				"EagerElementTrigger.csproj",
				"<Project><PropertyGroup>$",
				"<Fo>Tr\n",
				"<Project><PropertyGroup><Foo>True$</Foo>"
			);

		[Test]
		public Task CommitClosingTagAtEof ()
			=> TestTypeCommands (
				"ClosingTag.csproj",
				"<Project>\n  <PropertyGroup>\n    <foo>hello</foo>\n  $",
				"</Prop\n",
				"<Project>\n  <PropertyGroup>\n    <foo>hello</foo>\n  </PropertyGroup>$"
			);

		[Test]
		public Task CommitClosingTag ()
			=> TestTypeCommands (
				"ClosingTag.csproj",
				"<Project>\n  <PropertyGroup>\n    <foo>hello</foo>\n  $\n</Project>\n",
				"</Prop\n",
				"<Project>\n  <PropertyGroup>\n    <foo>hello</foo>\n  </PropertyGroup>$\n</Project>\n"
			);
	}
}
