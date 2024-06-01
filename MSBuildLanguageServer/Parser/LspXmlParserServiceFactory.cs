// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Composition;
using Microsoft.CodeAnalysis.LanguageServer;
using Microsoft.CodeAnalysis.LanguageServer.Handler;
using Microsoft.CommonLanguageServerProtocol.Framework;

using MonoDevelop.MSBuild.Editor.LanguageServer.Workspace;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Parser;

[ExportCSharpVisualBasicLspServiceFactory(typeof(LspXmlParserService)), Shared]
class LspXmlParserServiceFactory : ILspServiceFactory
{
    public ILspService CreateILspService(LspServices lspServices, WellKnownLspServerKinds serverKind)
    {
        var logger = lspServices.GetRequiredService<ILspLogger>();
        var workspace = lspServices.GetRequiredService<LspEditorWorkspace>();
        return new LspXmlParserService(logger, workspace);
    }
}
