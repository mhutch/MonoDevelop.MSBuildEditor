// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Composition;

using Microsoft.CodeAnalysis.LanguageServer;
using Microsoft.CodeAnalysis.LanguageServer.Handler;

using MonoDevelop.MSBuild.Editor.CodeActions;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Services;

[ExportCSharpVisualBasicLspServiceFactory(typeof(CodeActionCache)), Shared]
internal class CodeActionCacheFactory : ILspServiceFactory
{
    [ImportingConstructor]
    public CodeActionCacheFactory()
    {
    }

    public ILspService CreateILspService(LspServices lspServices, WellKnownLspServerKinds serverKind) => new CodeActionCache();
}

class CodeActionCache : ResolveCache<List<MSBuildCodeAction>>
{
    public CodeActionCache() : base(maxCacheSize: 3) { }
}
