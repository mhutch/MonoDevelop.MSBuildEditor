// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Utilities;

namespace MonoDevelop.MSBuild.Editor.Commands
{
	[Export (typeof (ICommandHandler))]
	[ContentType (MSBuildContentType.Name)]
	[Name ("MSBuild Go to Definition")]
	class MSBuildGoToDefinitionCommandHandler : ICommandHandler<GoToDefinitionCommandArgs>
	{
		public string DisplayName { get; } = "Go to Definition";

		public bool ExecuteCommand (GoToDefinitionCommandArgs args, CommandExecutionContext executionContext)
		{
			return false;
		}

		public CommandState GetCommandState (GoToDefinitionCommandArgs args)
		{
			return CommandState.Unavailable;
		}
	}
}
