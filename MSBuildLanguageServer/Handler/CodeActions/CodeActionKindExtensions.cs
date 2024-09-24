// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.MSBuild.Editor.CodeActions;

using Roslyn.LanguageServer.Protocol;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler.CodeActions;

static class CodeActionKindExtensions
{
    public static ISet<MSBuildCodeActionKind> GetMSBuildCodeActionKinds(this CodeActionKind[]? rawKinds)
    {
        var kinds = new HashSet<MSBuildCodeActionKind>();

        if(rawKinds is null)
        {
            return kinds;
        }

        foreach(var kind in rawKinds)
        {
            switch(kind.Value)
            {
            case "":
                kinds.Clear();
                return kinds;
            case "quickfix":
                kinds.Add(MSBuildCodeActionKind.CodeFix);
                break;
            case "refactor":
                kinds.Add(MSBuildCodeActionKind.Refactoring);
                break;
            case "refactor.inline":
                kinds.Add(MSBuildCodeActionKind.RefactoringInline);
                break;
            case "refactor.extract":
                kinds.Add(MSBuildCodeActionKind.RefactoringExtract);
                break;
            }
        }

        return kinds;
    }

    public static CodeActionKind GetLspCodeActionKind(this MSBuildCodeAction codeAction) =>
        codeAction.Kind switch {
            MSBuildCodeActionKind.CodeFix => CodeActionKind.QuickFix,
            MSBuildCodeActionKind.ErrorFix => CodeActionKind.QuickFix,
            MSBuildCodeActionKind.StyleFix => CodeActionKind.QuickFix,
            MSBuildCodeActionKind.Refactoring => CodeActionKind.Refactor,
            MSBuildCodeActionKind.RefactoringExtract => CodeActionKind.RefactorExtract,
            MSBuildCodeActionKind.RefactoringInline => CodeActionKind.RefactorInline,
            // TODO: log warning
            _ => codeAction.FixesDiagnostics.Count > 0 ? CodeActionKind.QuickFix : CodeActionKind.Refactor,
        };

}