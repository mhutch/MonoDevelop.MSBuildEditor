// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TaskStatusCenter;

using MonoDevelop.Xml.Editor.Parsing;

namespace MonoDevelop.MSBuild.Editor.VisualStudio;

[Export(typeof (MSBuildTaskCenterProgressReporter))]
internal class MSBuildTaskCenterProgressReporter
{
	readonly SVsServiceProvider serviceProvider;
	readonly IBackgroundParseService parseService;
	IVsTaskStatusCenterService taskStatusCenterService;
	TaskCompletionSource<bool> parseTracker = new ();
	readonly object lockObj = new ();

	[ImportingConstructor]
	public MSBuildTaskCenterProgressReporter (SVsServiceProvider serviceProvider, BackgroundParseServiceProvider parseServiceProvider)
	{
		this.serviceProvider = serviceProvider;
		parseService = parseServiceProvider.GetParseServiceForContentType (MSBuildContentType.Name);
	}

	public IVsTaskStatusCenterService TaskCenter => taskStatusCenterService;

	public async Task InitializeAsync ()
	{
		var asyncServiceProvider = serviceProvider.GetService<SAsyncServiceProvider, IAsyncServiceProvider> ();
		taskStatusCenterService = await asyncServiceProvider.GetServiceAsync<SVsTaskStatusCenterService, IVsTaskStatusCenterService> ().ConfigureAwait (false);

		parseService.RunningStateChanged += RunningStateChanged;
	}

	void RunningStateChanged (object sender, EventArgs e)
	{
		lock (lockObj) {
			if (parseService.IsRunning) {
				if (parseTracker.Task.IsCompleted) {
					parseTracker = new ();
					var options = new TaskHandlerOptions {
						Title = "Updating MSBuild IntelliSense"
					};
					var progress = new TaskProgressData {
						CanBeCanceled = false,
					};
					var handler = taskStatusCenterService.PreRegister (options, progress);
					handler.RegisterTask (parseTracker.Task);
				}
			} else {
				parseTracker.TrySetResult (true);
			}
		}
	}
}