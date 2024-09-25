// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Text.Editor.Commanding;

using MonoDevelop.Xml.Editor.Tests.Extensions;

using NUnit.Framework;

namespace MonoDevelop.MSBuild.Tests.Editor.Completion
{
	[TestFixture]
	public class MSBuildCommitTests : MSBuildEditorTest
	{
		Task TestTypeCommands (string filename, string before, string typeChars, string after)
		{
			return this.TestCommands (
				before,
				after,
				[ (s) => s.Type (typeChars) ],
				filename: filename,
				initialize: (tv) => {
					tv.Options.SetOptionValue ("BraceCompletion/Enabled", true);
					return Task.CompletedTask;
				}
			);
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
