// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Streams;
using StreamJsonRpc;
using Microsoft.VisualStudio.Composition;
using Microsoft.CodeAnalysis.LanguageServer;
using Roslyn.LanguageServer.Protocol;
using System.Composition;

static async Task<CompositionConfiguration> CreateMefCatalog ()
{
	// support both System.Composition and System.ComponentModel.Composition
	var discovery = PartDiscovery.Combine(
		new AttributedPartDiscovery(Resolver.DefaultInstance, isNonPublicSupported: true),
		new AttributedPartDiscoveryV1(Resolver.DefaultInstance));

	var assemblyCatalog = ComposableCatalog.Create(Resolver.DefaultInstance);

	var assemblies = new [] {
		typeof(MonoDevelop.Xml.Editor.XmlContentTypeNames).Assembly, // MonoDevelop.Xml.Editor.dll
		typeof(MonoDevelop.MSBuild.Editor.MSBuildContentType).Assembly // MonoDevelop.MSBuild.Editor.dll
	};

	foreach (var assembly in assemblies) {
		var parts = await discovery.CreatePartsAsync(assembly);
		assemblyCatalog = assemblyCatalog.AddParts(parts);
	}

	return CompositionConfiguration.Create(assemblyCatalog);
}

var logger = new MSBuildLspLogger ();

var mefGraph = await CreateMefCatalog ();
var exportProvider = mefGraph.CreateExportProviderFactory ().CreateExportProvider ();

// TODO: return compositionConfiguration.CompositionErrors over JsonRpc

var (clientStream, serverStream) = FullDuplexStream.CreatePair();

var messageFormatter = new JsonMessageFormatter();
var jsonRpc = new JsonRpc(new HeaderDelimitedMessageHandler(serverStream, serverStream, messageFormatter));

AbstractLspServiceProvider lspServiceProvider = exportProvider.GetExportedValue<CSharpVisualBasicLspServiceProvider> ();
ICapabilitiesProvider capabilitiesProvider = exportProvider.GetExportedValue<MSBuildCapabilitiesProvider> ();

var server = new MSBuildLanguageServer(
	lspServiceProvider,
	jsonRpc,
	capabilitiesProvider,
	logger,
    new Microsoft.CodeAnalysis.Host.HostServices (),
	[ "MSBuild" ],
	WellKnownLspServerKinds.MSBuild);

jsonRpc.StartListening();
server.Initialize();

[Export(typeof(MSBuildCapabilitiesProvider)), Shared]
class MSBuildCapabilitiesProvider : ICapabilitiesProvider
{
	public ServerCapabilities GetCapabilities (ClientCapabilities clientCapabilities)
	{
		throw new NotImplementedException ();
	}
}

/*
class MSBuildLanguageServer : AbstractLanguageServer<RequestContext>, IOnInitialized
{
	readonly ImmutableDictionary<Type, ImmutableArray<Func<ILspServices, object>>> _baseServices;

	public MSBuildLanguageServer (JsonRpc jsonRpc, ILspLogger logger) : base (jsonRpc, logger)
	{
		Initialize();
	}

	public Task OnInitializedAsync (ClientCapabilities clientCapabilities, RequestContext context, CancellationToken cancellationToken)
	{
		throw new NotImplementedException ();
	}

	protected override ILspServices ConstructLspServices ()
	{
		throw new NotImplementedException ();
	}
}*/
/*
[Export (typeof (IMethodHandler))]
class InitializeHandler : IInitializeManager<InitializeParams, InitializeResult>
{
	public InitializeParams GetInitializeParams ()
	{
		throw new NotImplementedException ();
	}

	public InitializeResult GetInitializeResult ()
	{
		throw new NotImplementedException ();
	}

	public void SetInitializeParams (InitializeParams request)
	{
		throw new NotImplementedException ();
	}
}

[Export (typeof (IMethodHandler))]
class TextDocumentDidChangeHandler : ILspDocumentRequestHandler<DidChangeTextDocumentParams, object?>
{
	public bool MutatesSolutionState => throw new NotImplementedException ();

	public TextDocumentIdentifier GetTextDocumentIdentifier (DidChangeTextDocumentParams request) => request.TextDocument;

	[LanguageServerEndpoint(Methods.TextDocumentDidChangeName)]
	public Task<object?> HandleRequestAsync (DidChangeTextDocumentParams request, MSBuildRequestContext context, CancellationToken cancellationToken)
	{
		throw new NotImplementedException ();
	}
}

[Export (typeof (IMethodHandler))]
class DocClosedHandler : ILspNotificationHandler<DidCloseTextDocumentParams>
{
	public bool MutatesSolutionState => throw new NotImplementedException ();

	[LanguageServerEndpoint(Methods.TextDocumentDidChangeName)]
	public Task HandleNotificationAsync (DidCloseTextDocumentParams request, MSBuildRequestContext requestContext, CancellationToken cancellationToken)
	{
		throw new NotImplementedException ();
	}
}
*/