// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Composition;

using Microsoft.CodeAnalysis.LanguageServer.Handler;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CommonLanguageServerProtocol.Framework;

using MonoDevelop.MSBuild.Analysis;
using MonoDevelop.MSBuild.Editor.LanguageServer.Parser;
using MonoDevelop.Xml.Analysis;

using Roslyn.LanguageServer.Protocol;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler;


[ExportCSharpVisualBasicStatelessLspService(typeof(DocumentDiagnosticsHandler)), Shared]
[Method(Methods.TextDocumentDiagnosticName)]
sealed class DocumentDiagnosticsHandler : ILspServiceDocumentRequestHandler<DocumentDiagnosticParams, SumType<FullDocumentDiagnosticReport, UnchangedDocumentDiagnosticReport>?>
{
    public bool MutatesSolutionState => false;

    public bool RequiresLSPSolution => true;

    public TextDocumentIdentifier GetTextDocumentIdentifier(DocumentDiagnosticParams request) => request.TextDocument;

    public async Task<SumType<FullDocumentDiagnosticReport, UnchangedDocumentDiagnosticReport>?> HandleRequestAsync(DocumentDiagnosticParams request, RequestContext context, CancellationToken cancellationToken)
    {
        var msbuildParserService = context.GetRequiredService<LspMSBuildParserService>();
        var xmlParserService = context.GetRequiredService<LspXmlParserService>();
        var logger = context.GetRequiredService<ILspLogger>();
        var extLogger = logger.ToILogger();

        var document = context.GetRequiredDocument();

        if(!msbuildParserService.TryGetParseResult(document.CurrentState, out Task<MSBuildParseResult>? parseTask, cancellationToken))
        {
            return null;
        }

        var result = await parseTask!; // not sure why we need the ! here, TryGetParseResult has NotNullWhen(true)


        var msbuildDoc = result.MSBuildDocument;
        var sourceText = result.XmlParseResult.Text;

        IEnumerable<MSBuildDiagnostic>? msbuildDiagnostics = msbuildDoc.Diagnostics;

        if(msbuildDoc.ProjectElement is not null)
        {
            try
            {
                // FIXME move this to a service
                var analyzerDriver = new MSBuildAnalyzerDriver(context.GetRequiredService<ILspLogger> ().ToILogger ());
                msbuildDiagnostics = await Task.Run(() => analyzerDriver.Analyze(msbuildDoc, true, cancellationToken), cancellationToken);
            } catch(Exception ex)
            {
                logger.LogException(ex, "Error in analyzer service");
            }
        }

        var convertedXmlDiagnostics = result.XmlParseResult.Diagnostics?.Select(d => ConvertDiagnostic(d, sourceText)) ?? [];
        var convertedMSBuildDiagnostics = msbuildDoc.Diagnostics.Select(d => ConvertDiagnostic(d, sourceText));

        return new FullDocumentDiagnosticReport {
            Items = convertedMSBuildDiagnostics.Concat(convertedXmlDiagnostics).ToArray()
        };
    }

    const string sourceName = "msbuild";

    static Diagnostic ConvertDiagnostic(XmlDiagnostic xmlDiagnostic, SourceText sourceText)
    {
        return new Diagnostic {
            Range = xmlDiagnostic.Span.ToLspRange(sourceText),
            Message = xmlDiagnostic.GetFormattedMessageWithTitle(),
            Severity = ConvertSeverity(xmlDiagnostic.Descriptor.Severity),
            Source = sourceName
            // don't use ID and code, it's generally too long and not useful
            // Code = xmlDiagnostic.Descriptor.Id,
        };
    }

    static DiagnosticSeverity ConvertSeverity(XmlDiagnosticSeverity severity) => severity switch {
        XmlDiagnosticSeverity.Suggestion => DiagnosticSeverity.Information,
        XmlDiagnosticSeverity.Warning => DiagnosticSeverity.Warning,
        XmlDiagnosticSeverity.Error => DiagnosticSeverity.Error,
        _ => throw new ArgumentException($"Unsupported XmlDiagnosticSeverity '{0}'")
    };

    static Diagnostic ConvertDiagnostic(MSBuildDiagnostic msbuildDiagnostic, SourceText sourceText)
    {
        return new Diagnostic {
            Range = msbuildDiagnostic.Span.ToLspRange(sourceText),
            Message = msbuildDiagnostic.GetFormattedMessageWithTitle(),
            Severity = ConvertSeverity(msbuildDiagnostic.Descriptor.Severity),
            Source = sourceName
            // don't use ID and code, it's generally too long and not useful
            //Code = msbuildDiagnostic.Descriptor.Id,
        };
    }

    static DiagnosticSeverity ConvertSeverity(MSBuildDiagnosticSeverity severity) => severity switch {
        MSBuildDiagnosticSeverity.Suggestion => DiagnosticSeverity.Information,
        MSBuildDiagnosticSeverity.Warning => DiagnosticSeverity.Warning,
        MSBuildDiagnosticSeverity.Error => DiagnosticSeverity.Error,
        _ => throw new ArgumentException($"Unsupported MSBuildDiagnosticSeverity '{0}'")
    };
}
