// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
/*
using Nerdbank.Streams;
using StreamJsonRpc;
using Microsoft.VisualStudio.Composition;
using Microsoft.CodeAnalysis.LanguageServer;
using Roslyn.LanguageServer.Protocol;
using System.Composition;
using Microsoft.CodeAnalysis.LanguageServer.LanguageServer;


WindowsErrorReporting.SetErrorModeOnWindows();

var parser = ProgramHelpers.CreateCommandLineParser();
return await parser.Parse(args).InvokeAsync(CancellationToken.None);




var logger = new MSBuildLspLogger ();

var (clientStream, serverStream) = FullDuplexStream.CreatePair();

var messageFormatter = new JsonMessageFormatter();
var jsonRpc = new JsonRpc(new HeaderDelimitedMessageHandler(serverStream, serverStream, messageFormatter));

// TODO: return compositionConfiguration.CompositionErrors over JsonRpc
var catalog = await MSBuildLspCatalog.Create ();

ICapabilitiesProvider capabilitiesProvider = catalog.ExportProvider.GetExportedValue<MSBuildCapabilitiesProvider> ();
ILanguageServerFactory languageServerFactory = catalog.ExportProvider.GetExportedValue<ILanguageServerFactory> ();

var host = new LanguageServerHost (serverStream, serverStream, catalog.ExportProvider, logger);

host.Start();

var server = languageServerFactory.Create (
	jsonRpc,
	capabilitiesProvider,
	WellKnownLspServerKinds.MSBuild,
	logger,
    new Microsoft.CodeAnalysis.Host.HostServices ()
);

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