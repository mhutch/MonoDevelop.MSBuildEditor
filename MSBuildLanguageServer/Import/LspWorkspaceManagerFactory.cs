// based on
// https://raw.githubusercontent.com/dotnet/roslyn/b9c35f1021d2d9d40521474c388d49ee7580ec98/src/Features/LanguageServer/Protocol/Workspaces/LspWorkspaceManagerFactory.cs
// with some parts commented out with /* */ comments

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Composition;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.LanguageServer.Handler;
using Microsoft.CommonLanguageServerProtocol.Framework;

namespace Microsoft.CodeAnalysis.LanguageServer;

[ExportCSharpVisualBasicLspServiceFactory(typeof(LspWorkspaceManager)), Shared]
internal class LspWorkspaceManagerFactory : ILspServiceFactory
{
    /*
    private readonly LspWorkspaceRegistrationService _workspaceRegistrationService;
    */
    [ImportingConstructor]
    [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
    public LspWorkspaceManagerFactory(/*LspWorkspaceRegistrationService lspWorkspaceRegistrationService*/)
    {
        /*
        _workspaceRegistrationService = lspWorkspaceRegistrationService;
        */
    }

    public ILspService CreateILspService(LspServices lspServices, WellKnownLspServerKinds serverKind)
    {
        var logger = lspServices.GetRequiredService<AbstractLspLogger>();
        /*
        var miscFilesWorkspace = lspServices.GetService<LspMiscellaneousFilesWorkspace>();
        var languageInfoProvider = lspServices.GetRequiredService<ILanguageInfoProvider>();
        */
        var telemetryLogger = lspServices.GetRequiredService<RequestTelemetryLogger>();
        return new LspWorkspaceManager(logger, /*miscFilesWorkspace, _workspaceRegistrationService, languageInfoProvider,*/ telemetryLogger);
    }
}