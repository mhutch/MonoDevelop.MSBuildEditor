// based on https://github.com/dotnet/roslyn/blob/6a5f9d76235e678e07cc3884a3e2920b4fd275ee/src/LanguageServer/Protocol/Handler/Configuration/DidChangeConfigurationNotificationHandlerFactory.cs
//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Composition;
using Microsoft.CodeAnalysis.LanguageServer;
using Microsoft.CodeAnalysis.LanguageServer.Handler;
using Microsoft.CommonLanguageServerProtocol.Framework;

using MonoDevelop.Xml.Options;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler.Configuration;

[ExportCSharpVisualBasicLspServiceFactory(typeof(WorkspaceConfigurationDidChangeHandler)), Shared]
[method: ImportingConstructor]
class WorkspaceConfigurationDidChangeHandlerFactory(IGlobalOptionService lspOptionsService) : ILspServiceFactory
{
    public ILspService CreateILspService(LspServices lspServices, WellKnownLspServerKinds serverKind)
    {
        var logger = lspServices.GetRequiredService<ILspLogger>();
        var clientLanguageServerManager = lspServices.GetRequiredService<IClientLanguageServerManager>();
        return new WorkspaceConfigurationDidChangeHandler(logger, lspOptionsService, clientLanguageServerManager);
    }
}
