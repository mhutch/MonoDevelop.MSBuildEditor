// Copyright (c) Microsoft. ALl rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
