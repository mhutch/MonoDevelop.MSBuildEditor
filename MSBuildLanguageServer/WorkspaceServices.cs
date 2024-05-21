// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Host;

namespace MonoDevelop.MSBuild.Editor.LanguageServer;

public abstract class WorkspaceServices
{
    public abstract HostServices HostServices { get; }

    public abstract T? GetService<T>() where T : IWorkspaceService;

    public T GetRequiredService<T>() where T : IWorkspaceService
        => GetService<T>() ?? throw new InvalidOperationException($"Workspace does not provide service {typeof(T).FullName}.");
}