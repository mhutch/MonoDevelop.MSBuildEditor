// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using StreamJsonRpc;
using Microsoft.VisualStudio.Composition;
using Microsoft.CodeAnalysis.LanguageServer;
using System.Composition;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.CodeAnalysis.Host;
using Newtonsoft.Json;

[Export(typeof(ILanguageServerFactory)), Shared]
[method: ImportingConstructor]
class MSBuildLanguageServerFactory (CSharpVisualBasicLspServiceProvider lspServiceProvider) : ILanguageServerFactory
{
	readonly AbstractLspServiceProvider lspServiceProvider = lspServiceProvider;

	public AbstractLanguageServer<RequestContext> Create (
		JsonRpc jsonRpc,
		JsonSerializer jsonSerializer,
		ICapabilitiesProvider capabilitiesProvider,
		WellKnownLspServerKinds serverKind,
		AbstractLspLogger logger,
		HostServices hostServices)
	{
		return new Microsoft.CodeAnalysis.LanguageServer.MSBuildLanguageServer (
			lspServiceProvider,
			jsonRpc,
			jsonSerializer,
			capabilitiesProvider,
			logger,
			hostServices,
			[ "MSBuild" ],
			serverKind);
	}
}
