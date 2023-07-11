// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Utilities;

using MonoDevelop.MSBuild.Editor.Navigation;
using MonoDevelop.Xml.Editor.Logging;
using MonoDevelop.Xml.Logging;

namespace MonoDevelop.MSBuild.Editor.Commands
{
	[Export (typeof (ICommandHandler))]
	[ContentType (MSBuildContentType.Name)]
	[Name ("MSBuild Find References")]
	class MSBuildFindReferencesCommandHandler : ICommandHandler<FindReferencesCommandArgs>
	{
		readonly IEditorLoggerFactory loggerFactory;
		readonly MSBuildNavigationService navigationService;

		[ImportingConstructor]
		public MSBuildFindReferencesCommandHandler (MSBuildNavigationService navigationService, IEditorLoggerFactory loggerFactory)
		{
			this.navigationService = navigationService;
			this.loggerFactory = loggerFactory;
		}


		public string DisplayName { get; } = "Find References";


		// log
		public bool ExecuteCommand (FindReferencesCommandArgs args, CommandExecutionContext executionContext)
		{
			try {
				return navigationService.FindReferences (args.SubjectBuffer, args.TextView.Caret.Position.BufferPosition);
			} catch (Exception ex) {
				loggerFactory.GetLogger<MSBuildFindReferencesCommandHandler> (args.TextView).LogInternalException (ex);
				throw;
			}
		}

		public CommandState GetCommandState (FindReferencesCommandArgs args)
		{
			try {
				return GetCommandStateInternal (args);
			} catch (Exception ex) {
				loggerFactory.GetLogger<MSBuildFindReferencesCommandHandler> (args.TextView).LogInternalException (ex);
				throw;
			}
		}

		CommandState GetCommandStateInternal (FindReferencesCommandArgs args)
		{
			var pos = args.TextView.Caret.Position.BufferPosition;
			if (navigationService.CanFindReferences (args.SubjectBuffer, pos)) {
				return CommandState.Available;
			}

			// visible but disabled
			return new CommandState (true, false, false, true);
		}
	}
}
