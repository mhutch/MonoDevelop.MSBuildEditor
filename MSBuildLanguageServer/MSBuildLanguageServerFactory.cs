// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Composition;
using System.Text.Json;

using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.LanguageServer;
using Microsoft.CodeAnalysis.LanguageServer.Handler;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.VisualStudio.Composition;

using StreamJsonRpc;

[Export(typeof(ILanguageServerFactory)), Shared]
[method: ImportingConstructor]
class MSBuildLanguageServerFactory (CSharpVisualBasicLspServiceProvider lspServiceProvider) : ILanguageServerFactory
{
	readonly AbstractLspServiceProvider lspServiceProvider = lspServiceProvider;

	public AbstractLanguageServer<RequestContext> Create (
		JsonRpc jsonRpc,
		JsonSerializerOptions options,
		ICapabilitiesProvider capabilitiesProvider,
		WellKnownLspServerKinds serverKind,
		AbstractLspLogger logger,
		HostServices hostServices,
        AbstractTypeRefResolver? typeRefResolver = null)
	{
		return new Microsoft.CodeAnalysis.LanguageServer.MSBuildLanguageServer (
			lspServiceProvider,
			jsonRpc,
			options,
			capabilitiesProvider,
			logger,
			hostServices,
			[ "MSBuild" ],
			serverKind,
            typeRefResolver);
	}
}
