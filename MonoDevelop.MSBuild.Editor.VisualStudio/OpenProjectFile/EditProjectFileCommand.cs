// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;

using Task = System.Threading.Tasks.Task;

namespace MonoDevelop.MSBuild.Editor.VisualStudio
{
	[Order (10)]
	[AppliesTo ("OpenProjectFile")]
	[ExportCommandGroup ("{1496A755-94DE-11D0-8C3F-00C04FC2AAE2}")]
	internal sealed class EditProjectFileCommand : IAsyncCommandGroupHandler
	{
		[Import]
		private UnconfiguredProject UnconfiguredProject { get; set; }

		[Import (typeof (SVsServiceProvider))]
		private IServiceProvider ServiceProvider { get; set; }

		[Import]
		private IProjectThreadingService ProjectThreadingService { get; set; }

		public Task<CommandStatusResult> GetCommandStatusAsync (IImmutableSet<IProjectTree> nodes, long commandId, bool focused, string commandText, CommandStatus progressiveStatus)
		{
			if (commandId == 1632) {
				return Task.FromResult (new CommandStatusResult (true, commandText, CommandStatus.Enabled | CommandStatus.Supported));
			} else {
				return Task.FromResult (CommandStatusResult.Unhandled);
			}
		}

		public async Task<bool> TryHandleCommandAsync (IImmutableSet<IProjectTree> nodes, long commandId, bool focused, long commandExecuteOptions, IntPtr variantArgIn, IntPtr variantArgOut)
		{
			if (commandId != 1632) return false;

			await ProjectThreadingService.SwitchToUIThread ();
			var windowFrame = VsShellUtilities.OpenDocumentWithSpecificEditor (ServiceProvider, UnconfiguredProject.FullPath, new Guid (MSBuildEditorFactory.FactoryGuid), Guid.Empty);
			windowFrame?.Show ();

			return true;
		}
	}
}
