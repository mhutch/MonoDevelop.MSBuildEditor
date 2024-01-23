
using System.Composition;
using Microsoft.CodeAnalysis.LanguageServer;
using Roslyn.LanguageServer.Protocol;

// exporting this as ExperimentalCapabilitiesProvider is required for LanguageServerHost to pick it up

[Export(typeof(ExperimentalCapabilitiesProvider)), Shared]
class MSBuildCapabilitiesProvider : ExperimentalCapabilitiesProvider
{
	public ServerCapabilities GetCapabilities(ClientCapabilities clientCapabilities)
	{
		var capabilities = new ServerCapabilities ();
		return capabilities;
	}
}