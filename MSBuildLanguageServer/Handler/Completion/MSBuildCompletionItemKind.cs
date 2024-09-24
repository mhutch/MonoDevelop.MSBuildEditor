// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Typesystem;

using ProjectFileTools.NuGetSearch.Feeds;

using Roslyn.LanguageServer.Protocol;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler.Completion;

/// <summary>
/// Maps MSBuild completion items to LSP <see cref="CompletionItemKind"/>
/// </summary>
static class MSBuildCompletionItemKind
{
    public const CompletionItemKind Property = CompletionItemKind.Property;
    public const CompletionItemKind Item = CompletionItemKind.Class;
    public const CompletionItemKind Metadata = CompletionItemKind.Property;
    public const CompletionItemKind Function = CompletionItemKind.Function;

    public const CompletionItemKind Constant = CompletionItemKind.Constant;

    public const CompletionItemKind PropertySyntax = CompletionItemKind.Property;
    public const CompletionItemKind ItemSyntax = CompletionItemKind.Class;
    public const CompletionItemKind MetadataSyntax = CompletionItemKind.Property;

    public const CompletionItemKind Culture = CompletionItemKind.Constant;
    public const CompletionItemKind Sdk = CompletionItemKind.Module;

    public const CompletionItemKind NewGuid = CompletionItemKind.Macro;

    // TODO: icons
    public const CompletionItemKind PackageNuGet = CompletionItemKind.Module;
    public const CompletionItemKind PackageMyGet = CompletionItemKind.Module;
    public const CompletionItemKind PackageLocal = CompletionItemKind.Module;

    internal static CompletionItemKind GetCompletionItemKind(this ISymbol symbol)
        => symbol switch {
        PropertyInfo => Property,
        ItemInfo => Item,
        MetadataInfo => Metadata,
        ConstantSymbol => Constant,
        _ => CompletionItemKind.Element
        };

    internal static CompletionItemKind GetCompletionItemKind(this FeedKind feedKind)
        => feedKind switch {
            FeedKind.MyGet => PackageMyGet,
            FeedKind.Local => PackageLocal,
            _ => PackageNuGet
            };
}