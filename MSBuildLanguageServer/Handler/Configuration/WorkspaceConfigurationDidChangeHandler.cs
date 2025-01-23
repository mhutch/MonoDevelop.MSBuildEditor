// based on https://github.com/dotnet/roslyn/blob/6a5f9d76235e678e07cc3884a3e2920b4fd275ee/src/LanguageServer/Protocol/Handler/Configuration/DidChangeConfigurationNotificationHandler.cs
//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// CHANGES:
//
// * Use ClientLanguageServerManagerExtensions extension methods
// * Misc cleanup
//
// This also removes support for Roslyn's concept of 'per-language options' with language name prefix appended
// automatically, the MonoDevelop.Xml options model does not support per-language options.
//
// We may later want to add per-language options back in, but in an XML-specific way. For example, at the global
// option level we could generate prefixes such as `msbuild.xml_option_name`, which would be allowed to have no
// value. If a per-language option has no value, it would fall back to the "base" xml option, `xml_option_name`.
// This would not be exposed at the editorconfig level, as editorconfig would allow devs to scope a
// `xml_option_name` value to MSBuild files.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.LanguageServer;
using Microsoft.CodeAnalysis.LanguageServer.Handler;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Roslyn.LanguageServer.Protocol;
using Roslyn.Utilities;
using LSP = Roslyn.LanguageServer.Protocol;

using MonoDevelop.Xml.Options;
using MonoDevelop.MSBuild.Options;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler.Configuration;

[Method(Methods.WorkspaceDidChangeConfigurationName)]
partial class WorkspaceConfigurationDidChangeHandler : ILspServiceNotificationHandler<LSP.DidChangeConfigurationParams>
{
    private bool _supportWorkspaceConfiguration;
    private readonly ILspLogger _lspLogger;
    private readonly IGlobalOptionService _globalOptionService;
    private readonly IClientLanguageServerManager _clientLanguageServerManager;
    private readonly Guid _registrationId;

    /// <summary>
    /// All the <see cref="ConfigurationItem.Section"/> needs to be refreshed from the client.
    /// </summary>
    private readonly ImmutableArray<ConfigurationItem> _configurationItems;

    public WorkspaceConfigurationDidChangeHandler(
        ILspLogger logger,
        IGlobalOptionService globalOptionService,
        IClientLanguageServerManager clientLanguageServerManager)
    {
        _lspLogger = logger;
        _globalOptionService = globalOptionService;
        _clientLanguageServerManager = clientLanguageServerManager;
        _registrationId = Guid.NewGuid();
        _configurationItems = GenerateGlobalConfigurationItems();
    }

    public bool MutatesSolutionState => true;

    public bool RequiresLSPSolution => false;

    public Task HandleNotificationAsync(DidChangeConfigurationParams request, RequestContext requestContext, CancellationToken cancellationToken)
        => RefreshOptionsAsync(cancellationToken);

    private async Task RefreshOptionsAsync(CancellationToken cancellationToken)
    {
        // We rely on the workspace/configuration to get the option values. If client doesn't support this, don't update.
        if (!_supportWorkspaceConfiguration)
        {
            return;
        }

        var configurationsFromClient = await GetConfigurationsAsync(cancellationToken).ConfigureAwait(false);
        if (configurationsFromClient.IsEmpty)
        {
            // Failed to get values from client, do nothing.
            return;
        }

        RoslynDebug.Assert(configurationsFromClient.Length == SupportedOptions.Length);

        // LSP ensures the order of result from client should match the order we sent from server.
        using var _ = ArrayBuilder<KeyValuePair<IOption, object?>>.GetInstance(out var optionsToUpdate);

        for (var i = 0; i < configurationsFromClient.Length; i++)
        {
            var valueFromClient = configurationsFromClient[i];
            var option = SupportedOptions[i];

            // If option doesn't exist in the client, don't try to update the option.
            if (!string.IsNullOrEmpty(valueFromClient))
            {
                if (option.Serializer.TryParse(valueFromClient, out var parsedValue))
                {
                    optionsToUpdate.Add(KeyValuePairUtil.Create(option, parsedValue));
                }
                else
                {
                    _lspLogger.LogWarning($"Failed to parse '{valueFromClient}' to type: '{option.Type.Name}'. '{option.Name}' would not be updated.");
                }
            }
        }

        _globalOptionService.SetGlobalOptions(optionsToUpdate.ToImmutable());
    }

    private async Task<ImmutableArray<string?>> GetConfigurationsAsync(CancellationToken cancellationToken)
    {
        // Attempt to get configurations from the client.  If this throws we'll get NFW reports.
        var options = await _clientLanguageServerManager.GetConfigurationsAsync(_configurationItems.AsArray(), cancellationToken).ConfigureAwait(false);

        // Failed to get result from client.
        Contract.ThrowIfNull(options);
        var converted = options.SelectAsArray(token => token?.ToString());
        return converted;
    }

    /// <summary>
    /// Generate the configuration items send to the client.
    /// For each option, generate its full name. If the option is <see cref="ISingleValuedOption"/> it's is what we sent to the client.
    /// If it is <see cref="IPerLanguageValuedOption"/>, then generate two configurationItems with prefix visual_basic and csharp.
    /// </summary>
    private static ImmutableArray<ConfigurationItem> GenerateGlobalConfigurationItems()
    {
        using var _ = ArrayBuilder<ConfigurationItem>.GetInstance(out var builder);
        foreach (var option in SupportedOptions)
        {
            builder.Add(new ConfigurationItem()
            {
                Section = option.Name,
            });
        }

        return builder.ToImmutableAndClear();
    }

    // TODO: make this composable so Xml and MSBuild options can be defined in their own assemblies
    static readonly ImmutableArray<IOption> SupportedOptions = [
        ..TextFormattingOptions.GetAll(),
        ..XmlCompletionOptions.GetAll(),
        ..MSBuildCompletionOptions.GetAll(),
    ];
}
