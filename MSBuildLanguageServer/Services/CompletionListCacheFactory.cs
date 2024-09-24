// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Composition;

using Microsoft.CodeAnalysis.LanguageServer;
using Microsoft.CodeAnalysis.LanguageServer.Handler;

using MonoDevelop.MSBuild.Editor.LanguageServer.Handler.Completion;

using LSP = Roslyn.LanguageServer.Protocol;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Services;

[ExportCSharpVisualBasicLspServiceFactory(typeof(CompletionListCache)), Shared]
internal class CompletionListCacheFactory : ILspServiceFactory
{
    [ImportingConstructor]
    public CompletionListCacheFactory()
    {
    }

    public ILspService CreateILspService(LspServices lspServices, WellKnownLspServerKinds serverKind) => new CompletionListCache();
}

class CompletionListCache : ResolveCache<CompletionListCacheEntry>
{
    public CompletionListCache() : base(maxCacheSize: 3) { }
}

record CompletionListCacheEntry(List<ILspCompletionItem> Items, CompletionRenderContext Context) { }
