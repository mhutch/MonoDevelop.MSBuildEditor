// code fragments from
// https://github.com/dotnet/roslyn/blob/dd3795a49875bef2728f01e0284406f33ea1e2e1/src/Workspaces/Core/Portable/Workspace/Workspace.cs
// and
// https://github.com/dotnet/roslyn/blob/dd3795a49875bef2728f01e0284406f33ea1e2e1/src/Workspaces/Core/Portable/Workspace/Workspace_Events.cs

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.Host;
using Roslyn.Utilities;

namespace MonoDevelop.MSBuild.Editor.LanguageServer;

partial class Workspace
{
    readonly HostWorkspaceServices _services;
    readonly TaskQueue _taskQueue;

    internal string? Kind => "Host";

    protected Workspace(HostServices host)
    {
        _services = host.CreateWorkspaceServices(this);

        var schedulerProvider = _services.GetRequiredService<ITaskSchedulerProvider>();
        var listenerProvider = _services.GetRequiredService<IWorkspaceAsynchronousOperationListenerProvider>();
        _taskQueue = new TaskQueue(listenerProvider.GetListener(), schedulerProvider.CurrentContextScheduler);
    }

    public HostWorkspaceServices Services => _services;

    /// <summary>
    /// Executes an action as a background task, as part of a sequential queue of tasks.
    /// </summary>
    [SuppressMessage("Style", "VSTHRD200:Use \"Async\" suffix for async methods", Justification = "This is a Task wrapper, not an asynchronous method.")]
    protected internal Task ScheduleTask(Action action, string? taskName = "Workspace.Task")
        => _taskQueue.ScheduleTask(taskName ?? "Workspace.Task", action, CancellationToken.None);

    /// <summary>
    /// Execute a function as a background task, as part of a sequential queue of tasks.
    /// </summary>
    [SuppressMessage("Style", "VSTHRD200:Use \"Async\" suffix for async methods", Justification = "This is a Task wrapper, not an asynchronous method.")]
    protected internal Task<T> ScheduleTask<T>(Func<T> func, string? taskName = "Workspace.Task")
        => _taskQueue.ScheduleTask(taskName ?? "Workspace.Task", func, CancellationToken.None);
}
