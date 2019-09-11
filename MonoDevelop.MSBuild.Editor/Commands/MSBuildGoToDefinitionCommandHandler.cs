// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Utilities;

using MonoDevelop.MSBuild.Editor.Navigation;
using MonoDevelop.MSBuild.Language;

namespace MonoDevelop.MSBuild.Editor.Commands
{
	[Export (typeof (ICommandHandler))]
	[ContentType (MSBuildContentType.Name)]
	[Name (nameof (MSBuildGoToDefinitionCommandHandler))]
	class MSBuildGoToDefinitionCommandHandler : ICommandHandler<GoToDefinitionCommandArgs>
	{
		[Import]
		internal MSBuildNavigationService NavigationService { get; set; }

		public string DisplayName { get; } = "Go to Definition";

		public CommandState GetCommandState (GoToDefinitionCommandArgs args)
		{
			if (NavigationService.CanNavigate (args.SubjectBuffer, args.TextView.Caret.Position.BufferPosition, out var kind)) {
				if (kind == MSBuildReferenceKind.NuGetID) {
					return new CommandState (true, displayText: "Open on NuGet.org");
				}
				return CommandState.Available;
			}

			// visible but disabled
			return new CommandState (true, false, false, true);
		}

		public bool ExecuteCommand (GoToDefinitionCommandArgs args, CommandExecutionContext executionContext)
		{
			return NavigationService.Navigate (args.SubjectBuffer, args.TextView.Caret.Position.BufferPosition);
		}
	}
}
