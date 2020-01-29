// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;

using Task = System.Threading.Tasks.Task;

namespace MonoDevelop.MSBuild.Editor.VisualStudio
{
	[Order (10)]
	[AppliesTo ("OpenProjectFile")]
	[ExportCommandGroup ("{60481700-078B-11D1-AAF8-00A0C9055A90}")]
	internal sealed class OpenProjectEditorOnDefaultActionCommand : IAsyncCommandGroupHandler
	{
		[Import]
		private UnconfiguredProject UnconfiguredProject { get; set; }

		[Import (typeof (SVsServiceProvider))]
		private IServiceProvider ServiceProvider { get; set; }

		[Import]
		private IProjectThreadingService ProjectThreadingService { get; set; }

		public Task<CommandStatusResult> GetCommandStatusAsync (IImmutableSet<IProjectTree> nodes, long commandId, bool focused, string commandText, CommandStatus progressiveStatus)
		{
			CommandStatusResult result = CommandStatusResult.Unhandled;

			if (commandId == 2 || commandId == 3) {
				result = new CommandStatusResult (true, commandText, CommandStatus.Enabled);
			}

			return Task.FromResult (result);
		}

		public async Task<bool> TryHandleCommandAsync (IImmutableSet<IProjectTree> nodes, long commandId, bool focused, long commandExecuteOptions, IntPtr variantArgIn, IntPtr variantArgOut)
		{
			if (nodes != null && nodes.Count == 1 && nodes.First ().IsRoot ()) {
				if (!(commandId == 2 || commandId == 3)) return false;

				await ProjectThreadingService.SwitchToUIThread ();
				var windowFrame = VsShellUtilities.OpenDocumentWithSpecificEditor (ServiceProvider, UnconfiguredProject.FullPath, new Guid (MSBuildEditorFactory.FactoryGuid), Guid.Empty);
				windowFrame?.Show ();

				return true;
			} else {
				return false;
			}
		}
	}
}
