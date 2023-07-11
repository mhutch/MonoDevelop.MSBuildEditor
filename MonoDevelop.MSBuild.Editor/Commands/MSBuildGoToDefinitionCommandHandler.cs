// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Utilities;

using MonoDevelop.MSBuild.Editor.Navigation;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.Xml.Editor.Logging;
using MonoDevelop.Xml.Logging;

namespace MonoDevelop.MSBuild.Editor.Commands
{
	[Export (typeof (ICommandHandler))]
	[ContentType (MSBuildContentType.Name)]
	[Name (nameof (MSBuildGoToDefinitionCommandHandler))]
	class MSBuildGoToDefinitionCommandHandler : ICommandHandler<GoToDefinitionCommandArgs>
	{
		readonly MSBuildNavigationService navigationService;
		readonly IEditorLoggerFactory loggerFactory;

		[ImportingConstructor]
		public MSBuildGoToDefinitionCommandHandler (MSBuildNavigationService navigationService, IEditorLoggerFactory loggerFactory)
		{
			this.navigationService = navigationService;
			this.loggerFactory = loggerFactory;
		}

		public string DisplayName { get; } = "Go to Definition";

		public CommandState GetCommandState (GoToDefinitionCommandArgs args)
		{
			try {
				return GetCommandStateInternal (args);
			} catch (Exception ex) {
				loggerFactory.GetLogger<MSBuildGoToDefinitionCommandHandler> (args.TextView).LogInternalException (ex);
				throw;
			}
		}

		CommandState GetCommandStateInternal (GoToDefinitionCommandArgs args)
		{
			if (navigationService.CanNavigate (args.SubjectBuffer, args.TextView.Caret.Position.BufferPosition, out var kind)) {
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

			try {
				// note: this does not need the cancellation token because it created UI that handles cancellation
				return navigationService.Navigate (args.SubjectBuffer, args.TextView.Caret.Position.BufferPosition);
			} catch (Exception ex) {
				loggerFactory.GetLogger<MSBuildGoToDefinitionCommandHandler> (args.TextView).LogInternalException (ex);
				throw;
			}
		}
	}
}
