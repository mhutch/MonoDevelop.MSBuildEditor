// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MonoDevelop.Core.Text;
using MonoDevelop.Ide.CodeCompletion;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Ide.TypeSystem;
using MonoDevelop.Projects;

namespace MonoDevelop.MSBuildEditor.Tests
{
	//largely copied from MonoDevelop.AspNet.Tests.WebForms.WebFormsTesting
	// MIT License
	// Copyright (c) 2009 Novell, Inc. (http://www.novell.com)
	static class MSBuildEditorTesting
	{
		public static async Task<CompletionDataList> CreateProvider (string text, string extension, bool isCtrlSpace = false)
		{
			var result = await CreateEditor (text, extension);
			var textEditorCompletion = result.Extension;
			string editorText = result.EditorText;
			TestViewContent sev = result.ViewContent;
			int cursorPosition = text.IndexOf ('$');

			var ctx = textEditorCompletion.GetCodeCompletionContext (sev);

			if (isCtrlSpace)
				return await textEditorCompletion.HandleCodeCompletionAsync (ctx, CompletionTriggerInfo.CodeCompletionCommand) as CompletionDataList;
			else {
				var task = textEditorCompletion.HandleCodeCompletionAsync (ctx, new CompletionTriggerInfo (CompletionTriggerReason.CharTyped, editorText [cursorPosition - 1]));
				if (task != null) {
					return await task as CompletionDataList;
				}
				return null;
			}
		}

		struct CreateEditorResult
		{
			public MSBuildTestingEditorExtension Extension;
			public string EditorText;
			public TestViewContent ViewContent;
		}

		static async Task<CreateEditorResult> CreateEditor (string text, string extension)
		{
			string editorText;
			TestViewContent sev;
			string parsedText;
			int cursorPosition = text.IndexOf ('$');
			int endPos = text.IndexOf ('$', cursorPosition + 1);
			if (endPos == -1)
				parsedText = editorText = text.Substring (0, cursorPosition) + text.Substring (cursorPosition + 1);
			else {
				parsedText = text.Substring (0, cursorPosition) + new string (' ', endPos - cursorPosition) + text.Substring (endPos + 1);
				editorText = text.Substring (0, cursorPosition) + text.Substring (cursorPosition + 1, endPos - cursorPosition - 1) + text.Substring (endPos + 1);
				cursorPosition = endPos - 1;
			}

			var project = Services.ProjectService.CreateDotNetProject ("C#");
			project.References.Add (ProjectReference.CreateAssemblyReference ("System"));
			project.References.Add (ProjectReference.CreateAssemblyReference ("System.Web"));
			project.FileName = UnitTests.TestBase.GetTempFile (".csproj");
			string file = UnitTests.TestBase.GetTempFile (extension);
			project.AddFile (file);

			sev = new TestViewContent {
				Project = project,
				ContentName = file,
				Text = editorText,
				CursorPosition = cursorPosition
			};
			var tww = new TestWorkbenchWindow { ViewContent = sev };
			var doc = new TestDocument (tww);
			doc.Editor.FileName = sev.ContentName;
			var parser = new MSBuildDocumentParser ();
			var options = new ParseOptions {
				Project = project,
				FileName = sev.ContentName,
				Content = new StringTextSource (parsedText)
			};
			var parsedDoc = await parser.Parse (options, default (CancellationToken)) as MSBuildParsedDocument;
			doc.HiddenParsedDocument = parsedDoc;

			return new CreateEditorResult {
				Extension = new MSBuildTestingEditorExtension (doc),
				EditorText = editorText,
				ViewContent = sev
			};
		}

		public class MSBuildTestingEditorExtension : MSBuildTextEditorExtension
		{
			public MSBuildTestingEditorExtension (Document doc)
			{
				//HACK: the PackageManagement addin doesn't declare its dependencies correctly
				//so the addin engine doesn't load these, and we have to force-load them
				//so that we can use them
				var nugetAddinLocation = Path.GetDirectoryName (typeof (PackageManagement.PackageManagementServices).Assembly.Location);
				Core.Runtime.SystemAssemblyService.LoadAssemblyFrom (Path.Combine (nugetAddinLocation, "NuGet.Configuration.dll"));
				Core.Runtime.SystemAssemblyService.LoadAssemblyFrom (Path.Combine (nugetAddinLocation, "NuGet.Protocol.dll"));

				Initialize (doc.Editor, doc);
			}

			public CodeCompletionContext GetCodeCompletionContext (TestViewContent sev)
			{
				var ctx = new CodeCompletionContext { TriggerOffset = sev.CursorPosition };
				sev.GetLineColumnFromPosition (ctx.TriggerOffset, out int line, out int column);
				ctx.TriggerLine = line;
				ctx.TriggerLineOffset = column - 1;

				return ctx;
			}
		}
	}
}
