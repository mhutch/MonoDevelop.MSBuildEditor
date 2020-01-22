using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.ProjectSystem;

using Task = System.Threading.Tasks.Task;

namespace MonoDevelop.MSBuild.Editor.VisualStudio
{
	[Order (10)]
	[AppliesTo ("OpenProjectFile")]
	[ExportCommandGroup ("{60481700-078B-11D1-AAF8-00A0C9055A90}")]
	internal sealed class OpenProjectEditorOnDefaultActionCommand : IAsyncCommandGroupHandler
	{
		public Task<CommandStatusResult> GetCommandStatusAsync (IImmutableSet<IProjectTree> nodes, long commandId, bool focused, string commandText, CommandStatus progressiveStatus)
		{
			CommandStatusResult result = CommandStatusResult.Unhandled;

			if (commandId == 2 || commandId == 3) {
				result = new CommandStatusResult (true, commandText, CommandStatus.Enabled);
			}

			return Task.FromResult (result);
		}

		public Task<bool> TryHandleCommandAsync (IImmutableSet<IProjectTree> nodes, long commandId, bool focused, long commandExecuteOptions, IntPtr variantArgIn, IntPtr variantArgOut)
			=> Task.FromResult (true);
	}
}
