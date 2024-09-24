// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Composition;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.LanguageServer;
using Microsoft.CodeAnalysis.LanguageServer.Handler;
using Microsoft.CommonLanguageServerProtocol.Framework;

using MonoDevelop.MSBuild.Editor.LanguageServer;
using MonoDevelop.MSBuild.Editor.Roslyn;
using MonoDevelop.MSBuild.Language;

[ExportCSharpVisualBasicLspServiceFactory(typeof(FunctionTypeProviderService)), Shared]
class FunctionTypeProviderServiceFactory : ILspServiceFactory
{
    readonly IRoslynCompilationProvider compilationProvider;

    [ImportingConstructor]
    [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
    public FunctionTypeProviderServiceFactory(IRoslynCompilationProvider compilationProvider)
    {
        this.compilationProvider = compilationProvider;
    }

    public ILspService CreateILspService(LspServices lspServices, WellKnownLspServerKinds serverKind)
    {
        var logger = lspServices.GetRequiredService<ILspLogger>();
        return new FunctionTypeProviderService(compilationProvider, logger);
    }
}

class FunctionTypeProviderService : ILspService
{
    public IFunctionTypeProvider FunctionTypeProvider { get; }

    public FunctionTypeProviderService(IRoslynCompilationProvider compilationProvider, ILspLogger logger)
    {
        var log = logger.ToILogger();
        this.FunctionTypeProvider = new RoslynFunctionTypeProvider(compilationProvider, log);
    }
}

[Export(typeof(IRoslynCompilationProvider))]
class SimpleRoslynCompilationProvider : IRoslynCompilationProvider
{
    public MetadataReference CreateReference(string assemblyPath)
        => MetadataReference.CreateFromFile(assemblyPath);
}