// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Composition;

using Microsoft.CodeAnalysis.LanguageServer;
using Microsoft.CodeAnalysis.LanguageServer.Handler;
using Microsoft.CommonLanguageServerProtocol.Framework;

using MonoDevelop.MSBuild.Editor.LanguageServer.Workspace;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Services;

[ExportCSharpVisualBasicLspServiceFactory(typeof(LspNavigationService)), Shared]
internal class LspNavigationServiceFactory : ILspServiceFactory
{
    [ImportingConstructor]
    public LspNavigationServiceFactory()
    {
    }

    public ILspService CreateILspService(LspServices lspServices, WellKnownLspServerKinds serverKind)
    {
        var logger = lspServices.GetRequiredService<ILspLogger>();
        var extLogger = logger.ToILogger();
        var workspaceService = lspServices.GetRequiredService<LspEditorWorkspace>();
        var xmlParserService = lspServices.GetRequiredService<LspXmlParserService>();
        return new LspNavigationService(workspaceService, xmlParserService, extLogger);
    }
}
