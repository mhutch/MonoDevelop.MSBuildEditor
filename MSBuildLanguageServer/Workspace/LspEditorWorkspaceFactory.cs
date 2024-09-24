// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Composition;
using Microsoft.CodeAnalysis.LanguageServer;
using Microsoft.CodeAnalysis.LanguageServer.Handler;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Workspace;

[ExportCSharpVisualBasicLspServiceFactory(typeof(LspEditorWorkspace)), Shared]
class LspEditorWorkspaceFactory : ILspServiceFactory
{
    public ILspService CreateILspService(LspServices lspServices, WellKnownLspServerKinds serverKind)
    {
        return new LspEditorWorkspace();
    }
}
