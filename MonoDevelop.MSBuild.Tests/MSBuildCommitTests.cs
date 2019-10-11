// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.VisualStudio.MiniEditor;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding;

using MonoDevelop.MSBuild.Editor;
using MonoDevelop.Xml.Tests.Completion;
using MonoDevelop.Xml.Tests.EditorTestHelpers;

using NUnit.Framework;

namespace MonoDevelop.MSBuild.Tests
{
	[TestFixture]
	public class MSBuildCommitTests : CompletionTestBase
	{
		[OneTimeSetUp]
		public void LoadMSBuild () => MSBuildTestHelpers.RegisterMSBuildAssemblies ();

		protected override string ContentTypeName => MSBuildContentType.Name;

		protected override (EditorEnvironment, EditorCatalog) InitializeEnvironment () => MSBuildTestEnvironment.EnsureInitialized ();

		Task TestTypeCommands (string filename, string before, string typeChars, string after)
		{
			return TestCommands (
				before,
				after,
				new Action<IEditorCommandHandlerService>[] { (s) => s.Type (typeChars) },
				filename: filename,
				initialize: (ITextView tv) => {
					tv.Options.SetOptionValue ("BraceCompletion/Enabled", true);
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
	}
}
