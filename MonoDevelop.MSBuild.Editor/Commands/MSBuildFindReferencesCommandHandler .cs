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
	[Name ("MSBuild Find References")]
	class MSBuildFindReferencesCommandHandler : ICommandHandler<FindReferencesCommandArgs>
	{
		public string DisplayName { get; } = "Find References";

		public bool ExecuteCommand (FindReferencesCommandArgs args, CommandExecutionContext executionContext)
		{
			return false;
		}

		public CommandState GetCommandState (FindReferencesCommandArgs args)
		{
			return CommandState.Unavailable;
		}
	}
}
