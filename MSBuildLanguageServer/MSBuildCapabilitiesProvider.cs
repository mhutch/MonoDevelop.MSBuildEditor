
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
            CompletionProvider = new CompletionOptions {
                TriggerCharacters = [
                    // xml tag
                    "<",
                    // attribute quotes
                    "\"",
                    "'",
                    // expressions
                    "(",
                    // -> transforms and element values
                    ">",
                    // members in property functions
                    "."
                 ],
                ResolveProvider = true
            },
            DiagnosticOptions = new DiagnosticOptions { },
            DefinitionProvider = new DefinitionOptions { WorkDoneProgress = true },
            ReferencesProvider = new ReferenceOptions { WorkDoneProgress = true }
        };

        // our code action handler only supports code action literals i.e. returning CodeAction objects not Command objects,
        // so only register the handler if the client supports code action literals
        if(clientCapabilities.TextDocument?.CodeAction?.CodeActionLiteralSupport is not null)
        {
            capabilities.CodeActionProvider = new CodeActionOptions {
                CodeActionKinds = [
                    CodeActionKind.Refactor,
                    CodeActionKind.QuickFix
                ],
                ResolveProvider = true
            };
        }

        return capabilities;
    }
}