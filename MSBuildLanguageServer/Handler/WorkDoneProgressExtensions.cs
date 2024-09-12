// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Roslyn.LanguageServer.Protocol;

namespace MonoDevelop.MSBuild.Editor.LanguageServer.Handler;

static class WorkDoneProgressExtensions
{
    public static void Begin(this IProgress<WorkDoneProgress>? progress, string title, bool? cancellable = null, string? message = null, int? percentage = null)
    {
        progress.Report(new WorkDoneProgressBegin {
            Title = title,
            Cancellable = cancellable,
            Message = message,
            Percentage = percentage
        });
    }

    public static void Report(this IProgress<WorkDoneProgress> progress, bool? cancellable = null, string? message = null, int? percentage = null)
    {
        progress.Report(new WorkDoneProgressReport{
            Cancellable = cancellable,
            Message = message,
            Percentage = percentage
        });
    }

    public static void End(this IProgress<WorkDoneProgress>? progress, string? message = null)
    {
        progress.Report(new WorkDoneProgressEnd{
            Message = message
        });
    }
}

/*
static class ProgressReportingExtensionMethods
{
    public static IProgress<TResult> CreateProgress<TParams, TResult>(this RequestContext context, TParams p)
        where TParams : IPartialResultParams<TResult>
        where TResult : IPartialResult
    {
        var manager = context.GetRequiredLspService<IClientLanguageServerManager>();
        return new ProgressReporter<TResult>(manager, p.PartialResultToken, context.QueueCancellationToken);
    }

    class ProgressReporter<TResult> : IProgress<TResult>
        where TResult : IPartialResult
    {
        readonly IClientLanguageServerManager manager;
        readonly ProgressToken partialResultToken;
        readonly CancellationToken queueCancellationToken;

        public ProgressReporter(IClientLanguageServerManager manager, ProgressToken partialResultToken, CancellationToken queueCancellationToken)
        {
            this.manager = manager;
            this.partialResultToken = partialResultToken;
            this.queueCancellationToken = queueCancellationToken;
        }

        public void Report(TResult value)
        {
            value.PartialResultToken = partialResultToken;
            manager.SendNotificationAsync(Methods.ProgressNotificationName,  queueCancellationToken);
        }
    }
}
*/
/*

[ExportCSharpVisualBasicStatelessLspService(typeof(FindAllReferencesHandler)), Shared]
[Method(Methods.TextDocumentDocumentHighlightName)]
sealed class DocumentHighlightHandler : ILspServiceDocumentRequestHandler<DocumentHighlightParams, DocumentHighlight[]?>
{
    public bool MutatesSolutionState => false;

    public bool RequiresLSPSolution => true;

    public TextDocumentIdentifier GetTextDocumentIdentifier(DocumentHighlightParams request) => request.TextDocument;

    public async Task<DocumentHighlight[]?> HandleRequestAsync(DocumentHighlightParams request, RequestContext context, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}

[ExportCSharpVisualBasicStatelessLspService(typeof(FindAllReferencesHandler)), Shared]
[Method(Methods.TextDocumentDocumentLinkName)]
sealed class DocumentLinkHandler : ILspServiceDocumentRequestHandler<DocumentLinkParams, DocumentLink[]?>
{
    public bool MutatesSolutionState => false;

    public bool RequiresLSPSolution => true;

    public TextDocumentIdentifier GetTextDocumentIdentifier(DocumentLinkParams request) => request.TextDocument;

    public async Task<DocumentLink[]?> HandleRequestAsync(DocumentLinkParams request, RequestContext context, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}

[ExportCSharpVisualBasicStatelessLspService(typeof(FindAllReferencesHandler)), Shared]
[Method(Methods.DocumentLinkResolveName)]
sealed class DocumentLinkResolveHandler : ILspServiceDocumentRequestHandler<DocumentLink, DocumentLink>
{
    public bool MutatesSolutionState => false;

    public bool RequiresLSPSolution => true;

    public TextDocumentIdentifier GetTextDocumentIdentifier(DocumentLink request) => request.TextDocument;

    public async Task<DocumentLink> HandleRequestAsync(DocumentLink request, RequestContext context, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}

*/