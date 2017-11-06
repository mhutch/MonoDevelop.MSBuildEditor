//
// MSBuildCommands.cs
//
// Author:
//       Mikayla Hutchinson <m.j.hutchinson@gmail.com>
//
// Copyright (c) 2017 Microsoft Corp.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using MonoDevelop.Components.Commands;
using MonoDevelop.Core;
using MonoDevelop.Ide;
using MonoDevelop.Refactoring;

namespace MonoDevelop.MSBuildEditor
{
	public enum MSBuildCommands
	{
		NavigationOperations
	}

	sealed class MSBuildNavigationOperationsCommandHandler : CommandHandler
	{
		protected override void Run (object dataItem)
		{
			((Action)dataItem)();
		}

		protected override void Update (CommandArrayInfo info)
		{
			var doc = IdeApp.Workbench.ActiveDocument;
			if (doc == null || doc.FileName == FilePath.Null || doc.ParsedDocument == null)
				return;

			var msbuildEditor = doc.GetContent<MSBuildTextEditorExtension> ();
			if (msbuildEditor == null) {
				return;
			}

			CommandInfo goToDeclarationCommand = IdeApp.CommandService.GetCommandInfo (RefactoryCommands.GotoDeclaration);
			CommandInfo findReferenceCommand = IdeApp.CommandService.GetCommandInfo (RefactoryCommands.FindReferences);

			if (goToDeclarationCommand.Enabled) {
				info.Add (goToDeclarationCommand, new Action (() => IdeApp.CommandService.DispatchCommand (RefactoryCommands.GotoDeclaration)));
			}

			if (findReferenceCommand.Enabled) {
				info.Add (findReferenceCommand, new Action (() => IdeApp.CommandService.DispatchCommand (RefactoryCommands.FindReferences)));
			}
		}
	}
}
