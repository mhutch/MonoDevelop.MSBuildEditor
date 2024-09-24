// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Composition;

using Microsoft.CodeAnalysis.LanguageServer.Handler;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CommonLanguageServerProtocol.Framework;

using MonoDevelop.MSBuild.Analysis;
using MonoDevelop.MSBuild.Editor.LanguageServer.Parser;
using MonoDevelop.MSBuild.Language;
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

        var convertedXmlDiagnostics = result.XmlParseResult.Diagnostics?.Select(d => d.ToLspDiagnostic (sourceText)) ?? [];
        var convertedMSBuildDiagnostics = msbuildDoc.Diagnostics.Select(d => d.ToLspDiagnostic (sourceText));

        return new FullDocumentDiagnosticReport {
            Items = convertedMSBuildDiagnostics.Concat(convertedXmlDiagnostics).ToArray()
        };
    }

}

static class MSBuildDiagnosticExtensions
{
    const string sourceName = "msbuild";

    public static Diagnostic ToLspDiagnostic(this XmlDiagnostic xmlDiagnostic, SourceText sourceText, string sourceName = sourceName)
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

    public static Diagnostic ToLspDiagnostic(this MSBuildDiagnostic msbuildDiagnostic, SourceText sourceText, string sourceName = sourceName)
    {
        DiagnosticTag[]? diagnosticTags = msbuildDiagnostic.Descriptor.Id switch {
            CoreDiagnostics.DeprecatedWithMessage_Id => [DiagnosticTag.Deprecated],
            CoreDiagnostics.RedundantMinimumVersion_Id => [DiagnosticTag.Unnecessary],
            _ => null
        };

        return new Diagnostic {
            Range = msbuildDiagnostic.Span.ToLspRange(sourceText),
            Message = msbuildDiagnostic.GetFormattedMessageWithTitle(),
            Severity = ConvertSeverity(msbuildDiagnostic.Descriptor.Severity),
            Tags = diagnosticTags,
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
