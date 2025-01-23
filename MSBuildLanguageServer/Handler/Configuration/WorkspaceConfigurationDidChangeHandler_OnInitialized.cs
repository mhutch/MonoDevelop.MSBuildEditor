// based on https://github.com/dotnet/roslyn/blob/6a5f9d76235e678e07cc3884a3e2920b4fd275ee/src/LanguageServer/Protocol/Handler/Configuration/DidChangeConfigurationNotificationHandler_OnInitialized.cs
//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis.LanguageServer.Handler;
using Roslyn.LanguageServer.Protocol;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler.Configuration;

partial class WorkspaceConfigurationDidChangeHandler
{
    public async Task OnInitializedAsync(ClientCapabilities clientCapabilities, RequestContext context, CancellationToken cancellationToken)
    {
        if (clientCapabilities.Workspace?.DidChangeConfiguration?.DynamicRegistration is true)
        {
            await _clientLanguageServerManager.RegisterCapabilityAsync(
                new Registration {
                    Id = _registrationId.ToString(),
                    Method = Methods.WorkspaceDidChangeConfigurationName,
                    RegisterOptions = null
                },
                cancellationToken
            ).ConfigureAwait(false);
        }

        _supportWorkspaceConfiguration = clientCapabilities.Workspace?.Configuration ?? false;
        await RefreshOptionsAsync(cancellationToken).ConfigureAwait(false);
    }
}
