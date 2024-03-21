// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.LanguageServer;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Roslyn.LanguageServer.Protocol;

readonly struct RequestContext (ILspServices services, ILspLogger logger)
{
	public ILspServices Services { get; } = services;
	public ILspLogger Logger { get; } = logger;
	public WellKnownLspServerKinds ServerKind => WellKnownLspServerKinds.MSBuild;

	public static async Task<RequestContext> CreateAsync(
		bool mutatesSolutionState,
		bool requiresLSPSolution,
		TextDocumentIdentifier? textDocument,
		WellKnownLspServerKinds serverKind,
		ClientCapabilities? clientCapabilities,
		ImmutableArray<string> supportedLanguages,
		ILspServices lspServices,
		ILspLogger logger,
		string method,
		CancellationToken cancellationToken)
		=> throw new NotImplementedException();

	public ClientCapabilities GetRequiredClientCapabilities ()
	{
		throw new NotImplementedException ();
	}

	public T GetRequiredLspService<T>() where T : class, ILspService
    {
        return services.GetRequiredService<T>();
    }

    public T GetRequiredService<T>() where T : class
    {
        return services.GetRequiredService<T>();
    }

    public IEnumerable<T> GetRequiredServices<T>() where T : class
    {
        return services.GetRequiredServices<T>();
    }
}
