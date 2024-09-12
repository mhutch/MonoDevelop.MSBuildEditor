// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.LanguageServer;
using Microsoft.CodeAnalysis.LanguageServer.Handler;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;

using MonoDevelop.MSBuild.Editor.LanguageServer.Handler;
using MonoDevelop.MSBuild.Editor.LanguageServer.Parser;
using MonoDevelop.MSBuild.Editor.LanguageServer.Workspace;
using MonoDevelop.MSBuild.Editor.Navigation;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Parser;

using Roslyn.LanguageServer.Protocol;
using Roslyn.Utilities;

using LSP = Roslyn.LanguageServer.Protocol;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Services;

partial class LspNavigationService(LspEditorWorkspace workspace, LspXmlParserService xmlParserService, ILogger logger) : ILspService
{
    public Task FindReferences(
        MSBuildParseResult originParseResult,
        MSBuildReferenceCollectorFactory collectorFactory,
        BufferedProgress<Location[]> resultReporter,
        IProgress<WorkDoneProgress>? progress,
        CancellationToken cancellationToken,
        Func<FindReferencesResult, bool>? resultFilter = null)
    {
        return FindReferencesInternal(
            originParseResult,
            ReferencesToLocations,
            collectorFactory,
            resultReporter,
            progress,
            cancellationToken,
            resultFilter
        );
    }

    public Task FindReferences(
        MSBuildParseResult originParseResult,
        LSP.Range originRange,
        MSBuildReferenceCollectorFactory collectorFactory,
        BufferedProgress<LocationLink[]> resultReporter,
        IProgress<WorkDoneProgress>? progress,
        CancellationToken cancellationToken,
        Func<FindReferencesResult, bool>? resultFilter = null)
    {
        return FindReferencesInternal(
            originParseResult,
            (filename, sourceText, references) => ReferencesToLocationLinks(filename, sourceText, originRange, references),
            collectorFactory,
            resultReporter,
            progress,
            cancellationToken,
            resultFilter
        );
    }


    async Task FindReferencesInternal<TResult>(
        MSBuildParseResult originParseResult,
        Func<string, SourceText, List<FindReferencesResult>, TResult[]> createResults,
        MSBuildReferenceCollectorFactory collectorFactory,
        BufferedProgress<TResult[]> resultReporter,
        IProgress<WorkDoneProgress>? progress,
        CancellationToken cancellationToken,
        Func<FindReferencesResult, bool>? resultFilter = null)
    {
        var openDocuments = workspace.OpenDocuments.ToDictionary(d => d.FilePath, PathUtilities.Comparer);

        var originFilename = originParseResult.MSBuildDocument.Filename!;

        var jobs = originParseResult.MSBuildDocument.GetDescendentImports()
            .Where(imp => imp.IsResolved)
            .Select(imp => new FindReferencesSearchJob(imp.Filename!, null, null))
            .Prepend(
                new FindReferencesSearchJob(
                    originFilename,
                    originParseResult.XmlParseResult.XDocument,
                    originParseResult.XmlParseResult.Text))
            .ToList();

        int jobsCompleted = 0;
        object reportLock = new ();
        int percentageReported = 0;

        await Parallel.ForEachAsync(jobs, cancellationToken, async (job, token) => {
            try
            {
                var locations = await ProcessSearchJob(
                    originParseResult.MSBuildDocument,
                    createResults,
                    job,
                    openDocuments,
                    collectorFactory,
                    xmlParserService,
                    logger,
                    token
                    ).ConfigureAwait(false);

                if(locations is not null)
                {
                    resultReporter.Report(locations);
                }

                if(progress is not null)
                {
                    // increment the job completion count.
                    // if the increment causes the percentage to increase, then report it.
                    int updatedJobsCompleted = Interlocked.Increment(ref jobsCompleted);
                    int oldPercentage = (int)Math.Floor((double)(updatedJobsCompleted - 1) / jobs.Count);
                    int newPercentage = (int)Math.Floor((double)updatedJobsCompleted / jobs.Count);
                    if(newPercentage > oldPercentage)
                    {
                        lock(reportLock)
                        {
                            if (percentageReported < newPercentage)
                            {
                                percentageReported = newPercentage;
                                progress.Report(percentage: newPercentage);
                            }
                        }
                    }
                }

            } catch(Exception ex)
            {
                MSBuildNavigationHelpers.LogErrorSearchingFile(logger, ex, job.Filename);
            }
        });
    }

    static async Task<TResult[]?> ProcessSearchJob<TResult>(
        MSBuildRootDocument originDocument,
        Func<string, SourceText, List<FindReferencesResult>, TResult[]> createResults,
        FindReferencesSearchJob job,
        Dictionary<string, LspEditorDocument> openDocuments,
        MSBuildReferenceCollectorFactory collectorFactory,
        LspXmlParserService xmlParserService,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var filename = job.Filename;
        var sourceText = job.SourceText;
        var document = job.Document;

        if(sourceText is null)
        {
            if(openDocuments.TryGetValue(filename, out var openDocument))
            {
                sourceText = openDocument.CurrentState.Text.Text;
                if(xmlParserService.TryGetParseResult(openDocument.CurrentState, out var parseTask, cancellationToken))
                {
                    document = (await parseTask.ConfigureAwait(false)).XDocument;
                }
            } else
            {
                if(!File.Exists(filename))
                {
                    // TODO: log this?
                    return null;
                }
                using var file = File.OpenRead(filename);
                sourceText = SourceText.From(file);
            }
        }

        if(document is null)
        {
            var xmlParser = new XmlTreeParser(new XmlRootState());
            (document, _) = ParseSourceText(sourceText, xmlParserService.StateMachine, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();

        if(document.RootElement is null)
        {
            return null;
        }

        var references = new List<FindReferencesResult>();
        
        // the collector only uses the MSBuildDocument to resolve schemas,
        // so we can use the root document here.
        var collector = collectorFactory(originDocument, sourceText.GetTextSource(), logger, references.Add);
        collector.Run(document.RootElement, token: cancellationToken);

        if (references.Count == 0)
        {
            return null;
        }

        return createResults(filename, sourceText, references);
    }

    static (XDocument document, IReadOnlyList<Xml.Analysis.XmlDiagnostic>? diagnostics) ParseSourceText(SourceText text, XmlRootState stateMachine, CancellationToken cancellationToken)
    {
        var parser = new XmlTreeParser(stateMachine);
        var length = text.Length;
        for(int i = 0; i < length; i++)
        {
            parser.Push(text[i]);
            cancellationToken.ThrowIfCancellationRequested();
        }
        return parser.EndAllNodes();
    }

    static LocationLink[] ReferencesToLocationLinks(string targetFilePath, SourceText targetSourceText, LSP.Range originRange, List<FindReferencesResult> references)
    {
        var targetUri = ProtocolConversions.CreateAbsoluteUri(targetFilePath);

        var locations = references.Select(reference => {
            var range = targetSourceText.GetLspRange(reference.Offset, reference.Length);
            return new LocationLink {
                OriginSelectionRange = originRange,
                TargetUri = targetUri,
                TargetRange = range,
                TargetSelectionRange = range
            };
        }).ToArray();

        return locations;
    }

    static Location[] ReferencesToLocations(string targetFilePath, SourceText targetSourceText, List<FindReferencesResult> references)
    {
        var targetUri = ProtocolConversions.CreateAbsoluteUri(targetFilePath);

        var locations = references.Select(reference => {
            var range = targetSourceText.GetLspRange(reference.Offset, reference.Length);
            return new Location {
                Uri = targetUri,
                Range = range
            };
        }).ToArray();

        return locations;
    }
}
