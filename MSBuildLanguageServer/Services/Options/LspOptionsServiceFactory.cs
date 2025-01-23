// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Composition;
using Microsoft.CodeAnalysis.LanguageServer;
using Microsoft.CodeAnalysis.LanguageServer.Handler;
using Microsoft.CommonLanguageServerProtocol.Framework;

using MonoDevelop.MSBuild.Editor.LanguageServer.Services.Options;
using MonoDevelop.MSBuild.Editor.LanguageServer.Workspace;
using MonoDevelop.Xml.Options;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Services;

[ExportCSharpVisualBasicLspServiceFactory(typeof(LspDocumentOptionsService)), Shared]
class LspOptionsServiceFactory : ILspServiceFactory
{
    readonly IGlobalOptionService globalOptionService;

    [ImportingConstructor]
    public LspOptionsServiceFactory(IGlobalOptionService globalOptionService)
    {
        this.globalOptionService = globalOptionService;
    }

    public ILspService CreateILspService(LspServices lspServices, WellKnownLspServerKinds serverKind)
    {
        var logger = lspServices.GetRequiredService<ILspLogger>();
        var workspace = lspServices.GetRequiredService<LspEditorWorkspace>();

        return new LspDocumentOptionsService(logger, workspace, globalOptionService);
    }
}
