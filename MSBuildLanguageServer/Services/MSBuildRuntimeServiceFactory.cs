// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Composition;

using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.LanguageServer;
using Microsoft.CodeAnalysis.LanguageServer.Handler;
using Microsoft.CommonLanguageServerProtocol.Framework;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Services;

[ExportCSharpVisualBasicLspServiceFactory(typeof(MSBuildRuntimeService)), Shared]
class MSBuildRuntimeServiceFactory : ILspServiceFactory
{
    [ImportingConstructor]
    [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
    public MSBuildRuntimeServiceFactory()
    {
    }

    public ILspService CreateILspService(LspServices lspServices, WellKnownLspServerKinds serverKind)
    {
        var logger = lspServices.GetRequiredService<ILspLogger>();
        return new MSBuildRuntimeService(logger);
    }
}

// TODO: this is the beginning of an abstraction that will eventually allow us to load different MSBuild versions
class MSBuildRuntimeService : ILspService
{
    static object gate = new();
    static bool initialized = false;

    public MSBuildRuntimeService(ILspLogger logger)
    {
        if(!initialized)
        {
            lock(gate)
            {
                if(!initialized)
                {
                    initialized = true;
                    Microsoft.Build.Locator.MSBuildLocator.RegisterDefaults();
                }
            }
        }

        var log = logger.ToILogger();
        try
        {
            MSBuildEnvironment = new CurrentProcessMSBuildEnvironment(log);
        } catch(Exception ex)
        {
            logger.LogException(ex, "Failed to initialize MSBuild runtime info");
            MSBuildEnvironment = new NullMSBuildEnvironment();
        }
    }

    public IMSBuildEnvironment MSBuildEnvironment { get; }
}
