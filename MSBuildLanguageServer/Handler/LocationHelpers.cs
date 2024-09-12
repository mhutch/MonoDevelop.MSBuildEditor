// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.LanguageServer;
using Microsoft.CodeAnalysis.Text;

using MonoDevelop.MSBuild.Editor.LanguageServer.Parser;
using MonoDevelop.MSBuild.Editor.LanguageServer.Workspace;

using Roslyn.LanguageServer.Protocol;

using LSP = Roslyn.LanguageServer.Protocol;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler;

static class LocationHelpers
{
    public static SumType<Location, Location[], LocationLink[]>? ConvertLocationLinksToLocationsIfNeeded(LocationLink[]? results, bool supportsLocationLink)
    {
        if(results is null || results.Length == 0)
        {
            return null;
        }

        if(supportsLocationLink)
        {
            return results;
        }

        if(results.Length == 1)
        {
            return LocationLinkToLocation(results[0]);
        }

        return Array.ConvertAll(results, LocationLinkToLocation);
    }

    public static Location LocationLinkToLocation(LocationLink ll) => new Location {
        Uri = ll.TargetUri,
        Range = ll.TargetRange
    };

    public static LSP.Range ConvertRangeViaWorkspace(LspEditorWorkspace workspace, string filePath, Xml.Dom.TextSpan? targetSpan)
    {
        if(targetSpan is null)
        {
            return EmptyRange;
        }

        var uri = ProtocolConversions.CreateAbsoluteUri(filePath);
        SourceText? sourceText = null;
        if(workspace.GetTrackedLspText().TryGetValue(uri, out var tracked))
        {
            sourceText = tracked.Text;
        } else
        {
            sourceText = SourceText.From(filePath);
        }

        return sourceText.GetLspRange(targetSpan.Value.Start, targetSpan.Value.Length);
    }

    public static LocationLink CreateLocationLink(LSP.Range originRange, string targetPath, LSP.Range? targetRange = null, LSP.Range? targetSelectionRange = null)
    {
        targetRange ??= EmptyRange;
        return new LocationLink {
            OriginSelectionRange = originRange,
            TargetUri = ProtocolConversions.CreateAbsoluteUri(targetPath),
            TargetRange = targetRange,
            TargetSelectionRange = targetSelectionRange ?? targetRange
        };
    }

    public static readonly LSP.Range EmptyRange = new LSP.Range {
        Start = new LSP.Position { Line = 0, Character = 0 },
        End = new LSP.Position { Line = 0, Character = 0 }
    };
}
