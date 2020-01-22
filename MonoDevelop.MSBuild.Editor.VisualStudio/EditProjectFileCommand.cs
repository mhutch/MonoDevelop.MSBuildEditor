using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.VisualStudio.ProjectSystem;

using Task = System.Threading.Tasks.Task;

namespace MonoDevelop.MSBuild.Editor.VisualStudio
{
	[Order (10)]
	[AppliesTo ("OpenProjectFile")]
	[ExportCommandGroup ("{1496A755-94DE-11D0-8C3F-00C04FC2AAE2}")]
	internal sealed class EditProjectFileCommand : IAsyncCommandGroupHandler
	{
		public Task<CommandStatusResult> GetCommandStatusAsync (IImmutableSet<IProjectTree> nodes, long commandId, bool focused, string commandText, CommandStatus progressiveStatus)
		{
			if (commandId == 1632) {
				return Task.FromResult (new CommandStatusResult (true, commandText, CommandStatus.Invisible | CommandStatus.NotSupported));
			} else {
				return Task.FromResult (CommandStatusResult.Unhandled);
			}
		}

		public Task<bool> TryHandleCommandAsync (IImmutableSet<IProjectTree> nodes, long commandId, bool focused, long commandExecuteOptions, IntPtr variantArgIn, IntPtr variantArgOut)
			=> Task.FromResult (false);
	}
}
