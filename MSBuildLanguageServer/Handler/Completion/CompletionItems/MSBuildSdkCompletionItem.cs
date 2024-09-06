// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.MSBuild.SdkResolution;

using Roslyn.LanguageServer.Protocol;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler.Completion.CompletionItems;

class MSBuildSdkCompletionItem(SdkInfo info, MSBuildCompletionDocsProvider docsProvider) : ILspCompletionItem
{
    string label => info.Name;

    public bool IsMatch(CompletionItem request) => string.Equals(request.Label, label, StringComparison.Ordinal);

    public async ValueTask<CompletionItem> Render(CompletionRenderSettings settings, CompletionRenderContext ctx, CancellationToken cancellationToken)
    {
        var item = new CompletionItem { Label = label };

        if(settings.IncludeItemKind)
        {
            item.Kind = MSBuildCompletionItemKind.Sdk;
        }

        if(settings.IncludeDocumentation && info.Path is string sdkPath)
        {
            // FIXME: better docs
            item.Documentation = new MarkupContent { Kind = MarkupKind.Markdown, Value = $"`{sdkPath}`" };
        }

        return item;
    }
}
