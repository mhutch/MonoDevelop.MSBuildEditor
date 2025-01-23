// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis.LanguageServer;
using Roslyn.LanguageServer.Protocol;
using Roslyn.Utilities;

namespace MonoDevelop.MSBuild.Editor.LanguageServer;

static class ClientLanguageServerManagerExtensions
{
    /// <summary>
    /// Dynamically registers a single capability with the client via the <c>client/registerCapability<c> LSP method.
    /// </summary>
    /// <param name="clientLanguageServerManager">The client language server manager</param>
    /// <param name="registration">The capability to be registered</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>If registering multiple capabilities, use <see cref="RegisterCapabilitiesAsync(IClientLanguageServerManager, Registration[], CancellationToken)"/> instead.</remarks>
    public static Task RegisterCapabilityAsync(
        this IClientLanguageServerManager clientLanguageServerManager,
        Registration registration,
        CancellationToken cancellationToken)
        => clientLanguageServerManager.RegisterCapabilitiesAsync([ registration ], cancellationToken);

    /// <summary>
    /// Dynamically registers one or more capabilities with the client via the <c>client/registerCapability<c> LSP method.
    /// </summary>
    /// <param name="clientLanguageServerManager">The client language server manager</param>
    /// <param name="registrations">The capabilities to be registered</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task that represents the asynchronous operation.</returns>

    public static Task RegisterCapabilitiesAsync(
        this IClientLanguageServerManager clientLanguageServerManager,
        Registration[] registrations,
        CancellationToken cancellationToken)
    {
        return clientLanguageServerManager.SendRequestAsync<RegistrationParams, JsonElement>(
                Methods.ClientRegisterCapabilityName,
                new RegistrationParams { Registrations = registrations },
                cancellationToken);
    }

    /// <summary>
    /// Unregister a single dynamically registered capability with the client via the <c>client/unregisterCapability<c> LSP method.
    /// </summary>
    /// <param name="clientLanguageServerManager">The client language server manager</param>
    /// <param name="unregistration">The capability to be unregistered</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>If unregistering multiple capabilities, use <see cref="UnregisterCapabilitiesAsync(IClientLanguageServerManager, Unregistration[], CancellationToken)"/> instead.</remarks>

    public static Task UnregisterCapabilityAsync(
        this IClientLanguageServerManager clientLanguageServerManager,
        Unregistration unregistration,
        CancellationToken cancellationToken)
        => clientLanguageServerManager.UnregisterCapabilitiesAsync([ unregistration ], cancellationToken);

    /// <summary>
    /// Unregister one or more dynamically registered capabilities with the client via the <c>client/unregisterCapability<c> LSP method.
    /// </summary>
    /// <param name="clientLanguageServerManager">The client language server manager</param>
    /// <param name="unregistrations">The capabilities to be unregistered</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>If unregistering multiple capabilities, use <see cref="UnregisterCapabilitiesAsync(IClientLanguageServerManager, Unregistration[], CancellationToken)"/> instead.</remarks>

    public static Task UnregisterCapabilitiesAsync(
        this IClientLanguageServerManager clientLanguageServerManager,
        Unregistration[] unregistrations,
        CancellationToken cancellationToken)
    {
        return clientLanguageServerManager.SendRequestAsync<UnregistrationParams, JsonElement>(
                Methods.ClientUnregisterCapabilityName,
                new UnregistrationParams { Unregistrations = unregistrations },
                cancellationToken);
    }

    /// <summary>
    /// Fetch a single configuration setting from the client via the <c>workspace/configuration<c> LSP method.
    /// </summary>
    /// <param name="clientLanguageServerManager">The client language server manager</param>
    /// <param name="configurationItem">The configuration setting to fetch</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <remarks>If fetching multiple configuration settings, use <see cref="GetConfigurationsAsync(IClientLanguageServerManager, ConfigurationItem[], CancellationToken)"/> instead.</remarks>
    /// <returns>The configuration value</returns>
    public static async Task<JsonNode?> GetConfigurationAsync(
        this IClientLanguageServerManager clientLanguageServerManager,
        ConfigurationItem configurationItem,
        CancellationToken cancellationToken)
        {
            var result = await clientLanguageServerManager.GetConfigurationsAsync([ configurationItem ], cancellationToken).ConfigureAwait(false);
            Contract.ThrowIfNull(result);
            Contract.ThrowIfFalse(result.Count == 1);
            return result[0];
        }

    /// <summary>
    /// Fetch one or more configuration settings from the client via the <c>workspace/configuration<c> LSP method.
    /// </summary>
    /// <param name="clientLanguageServerManager">The client language server manager</param>
    /// <param name="configurationItem">The configuration setting to fetch</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>An array of the configuration values</returns>
    public static Task<JsonArray> GetConfigurationsAsync(
        this IClientLanguageServerManager clientLanguageServerManager,
        ConfigurationItem[] configurationItems,
        CancellationToken cancellationToken)
    {
        var result = clientLanguageServerManager.SendRequestAsync<ConfigurationParams, JsonArray>(
            Methods.WorkspaceConfigurationName,
            new ConfigurationParams { Items = configurationItems },
            cancellationToken);
        Contract.ThrowIfNull(result);
        return result;
    }
}