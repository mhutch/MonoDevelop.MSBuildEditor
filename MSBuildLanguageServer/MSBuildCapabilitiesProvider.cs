
using System.Composition;
using Microsoft.CodeAnalysis.LanguageServer;
using Roslyn.LanguageServer.Protocol;

// exporting this as ExperimentalCapabilitiesProvider is required for LanguageServerHost to pick it up

[Export(typeof(ExperimentalCapabilitiesProvider)), Shared]
class MSBuildCapabilitiesProvider : ExperimentalCapabilitiesProvider
{
    public ServerCapabilities GetCapabilities(ClientCapabilities clientCapabilities)
    {
        var capabilities = new ServerCapabilities {
            HoverProvider = true,
            TextDocumentSync = new TextDocumentSyncOptions {
                OpenClose = true,
                Change = TextDocumentSyncKind.Incremental
                // Save = true, // todo update mtime
            },
            DiagnosticOptions = new DiagnosticOptions { }
        };
        return capabilities;
    }
}