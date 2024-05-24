// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Composition;
using Microsoft.CodeAnalysis.LanguageServer;
using Microsoft.CodeAnalysis.LanguageServer.Handler;
using Microsoft.CommonLanguageServerProtocol.Framework;

using MonoDevelop.MSBuild.Editor.LanguageServer.Workspace;
using MonoDevelop.MSBuild.Schema;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Parser;

[ExportCSharpVisualBasicLspServiceFactory(typeof(LspMSBuildParserService)), Shared]
class LspMSBuildParserFactory : ILspServiceFactory
{
    ITaskMetadataBuilder taskMetadataBuilder;
    IMSBuildEnvironment msbuildEnvironment;
    MSBuildSchemaProvider schemaProvider;

    [ImportingConstructor]
    public LspMSBuildParserFactory(
        [Import(AllowDefault = true)] ITaskMetadataBuilder taskMetadataBuilder,
        [Import(AllowDefault = true)] MSBuildSchemaProvider schemaProvider,
        [Import(AllowDefault = true)] IMSBuildEnvironment msbuildEnvironment)
    {
        this.taskMetadataBuilder = taskMetadataBuilder ?? new NoopTaskMetadataBuilder();
        this.schemaProvider = schemaProvider ?? new MSBuildSchemaProvider();
        this.msbuildEnvironment = msbuildEnvironment;
    }

    public ILspService CreateILspService(LspServices lspServices, WellKnownLspServerKinds serverKind)
    {
        var logger = lspServices.GetRequiredService<ILspLogger>();
        var workspace = lspServices.GetRequiredService<LspEditorWorkspace>();
        var xmlParserService = lspServices.GetRequiredService<LspXmlParserService>();

        if(msbuildEnvironment == null)
        {
            var log = logger.ToILogger();
            try
            {
                msbuildEnvironment = new CurrentProcessMSBuildEnvironment(log);
            } catch(Exception ex)
            {
                logger.LogException(ex, "Failed to initialize MSBuild runtime info");
                msbuildEnvironment = new NullMSBuildEnvironment();
            }
        }

        return new LspMSBuildParserService(logger, workspace, xmlParserService, msbuildEnvironment, schemaProvider, taskMetadataBuilder);
    }
}
