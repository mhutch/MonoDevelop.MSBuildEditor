// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;

using MonoDevelop.MSBuild.Editor.Commands;
using MonoDevelop.MSBuild.Editor.Completion;
using MonoDevelop.MSBuild.Editor.Host;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.PackageSearch;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Editor;
using MonoDevelop.Xml.Editor.Logging;
using MonoDevelop.Xml.Logging;
using MonoDevelop.Xml.Parser;

using ProjectFileTools.NuGetSearch.Contracts;
using ProjectFileTools.NuGetSearch.Feeds;

namespace MonoDevelop.MSBuild.Editor.Navigation
{
	[Export, PartCreationPolicy (CreationPolicy.Shared)]
	partial class MSBuildNavigationService
	{
		[ImportingConstructor]
		public MSBuildNavigationService (
			IPackageSearchManager packageSearchManager,
			IMSBuildEditorHost editorHost,
			JoinableTaskContext joinableTaskContext,
			IStreamingFindReferencesPresenter presenter,
			MSBuildCachingResolver resolver,
			IContentTypeRegistryService contentTypeRegistry,
			ITextBufferFactoryService bufferFactory,
			MSBuildParserProvider parserProvider,
			IEditorLoggerFactory loggerService)
		{
			PackageSearchManager = packageSearchManager;
			EditorHost = editorHost;
			JoinableTaskContext = joinableTaskContext;
			Presenter = presenter;
			Resolver = resolver;
			ContentTypeRegistry = contentTypeRegistry;
			BufferFactory = bufferFactory;
			ParserProvider = parserProvider;
			LoggerService = loggerService;
		}

		public IPackageSearchManager PackageSearchManager { get; }
		public IMSBuildEditorHost EditorHost { get; }
		public JoinableTaskContext JoinableTaskContext { get; }
		public IStreamingFindReferencesPresenter Presenter { get; }
		public MSBuildCachingResolver Resolver { get; }
		public IContentTypeRegistryService ContentTypeRegistry { get; }
		public ITextBufferFactoryService BufferFactory { get; }
		public MSBuildParserProvider ParserProvider { get; }
		public IEditorLoggerFactory LoggerService { get; }

		public bool CanNavigate (ITextBuffer buffer, SnapshotPoint point) => CanNavigate (buffer, point, out _);

		public bool CanNavigate (ITextBuffer buffer, SnapshotPoint point, out MSBuildReferenceKind referenceKind)
		{
			Resolver.GetResolvedReference (buffer, point, out var doc, out var rr);

			if (MSBuildNavigation.CanNavigate (doc, point, rr)) {
				referenceKind = rr.ReferenceKind;
				return true;
			}

			referenceKind = MSBuildReferenceKind.None;
			return false;
		}

		public MSBuildNavigationResult GetNavigationResult (ITextBuffer buffer, SnapshotPoint point)
		{
			Resolver.GetResolvedReference (buffer, point, out var doc, out var rr);

			return MSBuildNavigation.GetNavigation (doc, point, rr);
		}

		public bool Navigate (ITextBuffer buffer, SnapshotPoint point)
		{
			var result = GetNavigationResult (buffer, point);
			if (result != null) {
				return Navigate (result, buffer);
			}
			return false;
		}

		// note: this does not need a cancellation token because it creates UI that handles cancellation of long-running work
		public bool Navigate (MSBuildNavigationResult result, ITextBuffer buffer)
		{
			var logger = GetLogger (buffer);
			if (result.Kind == MSBuildReferenceKind.Target) {
				FindTargetDefinitions (result.Name, buffer).LogTaskExceptionsAndForget (logger);
				return true;
			}

			if (result.Kind == MSBuildReferenceKind.Item) {
				FindItemWrites (result.Name, buffer).LogTaskExceptionsAndForget (logger);
				return true;
			}

			if (result.Kind == MSBuildReferenceKind.Property) {
				FindPropertyWrites (result.Name, buffer).LogTaskExceptionsAndForget (logger);
				return true;
			}

			if (result.Paths != null) {
				if (result.Paths.Length == 1) {
					EditorHost.OpenFile (result.Paths[0], 0);
					return true;
				}
				if (result.Paths.Length > 1) {
					ShowMultipleFiles (result.Paths, buffer, logger).LogTaskExceptionsAndForget (logger);
					return true;
				}
			}

			if (result.DestFile != null) {
				EditorHost.OpenFile (result.DestFile, result.TargetSpan?.Start ?? 0);
				return true;
			}

			if (result.Kind == MSBuildReferenceKind.NuGetID) {
				OpenNuGetUrl (result.Name, EditorHost, logger);
				return true;
			}

			return false;
		}

		ILogger GetLogger (ITextBuffer buffer) => LoggerService.GetLogger<MSBuildReferenceCollector>(buffer);

		void OpenNuGetUrl (string nuGetId, IMSBuildEditorHost host, ILogger logger)
		{
			Task.Run (async () => {
				var results = await PackageSearchManager.SearchPackageInfo (nuGetId, null, null).ToTask ();

				if (results.Any (r => r.SourceKind == FeedKind.NuGet)) {
					var url = $"https://www.nuget.org/packages/{Uri.EscapeDataString (nuGetId)}";
					Process.Start (url);
				} else {
					await JoinableTaskContext.Factory.SwitchToMainThreadAsync ();
					host.ShowStatusBarMessage ("Package is not from NuGet.org");
				}
			}).LogTaskExceptionsAndForget (logger);
		}

		async Task ShowMultipleFiles (string[] files, ITextBuffer buffer, ILogger logger)
		{
			var openDocuments = EditorHost.GetOpenDocuments ();
			var searchCtx = Presenter.StartSearch ($"Go to files", null, false);
			try {
				var msbuildContentType = ContentTypeRegistry.GetContentType (MSBuildContentType.Name);
				foreach (var file in files) {
				string lineText;
					try {
						if (!File.Exists (file)) {
							continue;
						}
						if (!openDocuments.TryGetValue (file, out var buf)) {
							buf = BufferFactory.CreateTextBuffer (File.OpenText (file), msbuildContentType);
						}
						lineText = buf.CurrentSnapshot.GetLineFromPosition (0).GetText ();

					} catch (Exception ex) {
						MSBuildNavigationHelpers.LogErrorGettingFileText (logger, ex, file);
						continue;
					}
					var classifiedSpans = ImmutableArray<ClassifiedText>.Empty;
					classifiedSpans = classifiedSpans.Add (new ClassifiedText (lineText, PredefinedClassificationTypeNames.NaturalLanguage));
					await searchCtx.OnReferenceFoundAsync (
						new FoundReference (
							file,
							0, 0,
							0, 0,
							ReferenceUsage.Declaration,
							classifiedSpans,
							new TextSpan (-1, 0)
						)
					);
				}
			} catch (Exception ex) when (!(ex is OperationCanceledException && searchCtx.CancellationToken.IsCancellationRequested)) {
				LogErrorShowingNavigateMultiple(logger, ex);
			}
			await searchCtx.OnCompletedAsync ();
		}

		public bool CanFindReferences (ITextBuffer buffer, SnapshotPoint point)
		{
			Resolver.GetResolvedReference (buffer, point, out _, out var rr);
			return MSBuildReferenceCollector.CanCreate (rr);
		}

		public bool FindReferences (ITextBuffer buffer, SnapshotPoint point)
		{
			Resolver.GetResolvedReference (buffer, point, out _, out var rr);
			return FindReferences (buffer, rr);
		}

		public bool FindReferences (ITextBuffer buffer, MSBuildResolveResult resolveResult)
		{
			if (!MSBuildReferenceCollector.CanCreate (resolveResult)) {
				return false;
			}
			var logger = LoggerService.GetLogger<MSBuildReferenceCollector> (buffer);
			FindReferencesAsync (buffer, resolveResult, logger).LogTaskExceptionsAndForget (logger);
			return true;
		}

		async Task FindReferencesAsync (ITextBuffer buffer, MSBuildResolveResult reference, ILogger logger)
		{
			var referenceName = reference.GetReferenceDisplayName ();

			string searchTitle = MSBuildNavigationHelpers.GetFindReferencesSearchTitle (reference, logger);

			var searchCtx = Presenter.StartSearch (searchTitle, referenceName, true);

			try {
				await FindReferences (searchCtx, (doc, text, logger, reporter) => MSBuildReferenceCollector.Create (doc, text, logger, reference, Resolver.FunctionTypeProvider, reporter), buffer);
			} catch (Exception ex) when (!(ex is OperationCanceledException && searchCtx.CancellationToken.IsCancellationRequested)) {
				LogErrorFindReferences (logger, ex);
			}
			await searchCtx.OnCompletedAsync ();
		}

		async Task FindTargetDefinitions (string targetName, ITextBuffer buffer)
		{
			var title = MSBuildNavigationHelpers.GetFindTargetDefinitionsSearchTitle (targetName);
			var searchCtx = Presenter.StartSearch (title, targetName, true);

			try {
				await FindReferences (searchCtx, (doc, text, logger, reporter) => new MSBuildTargetDefinitionCollector (doc, text, logger, targetName, reporter), buffer);
			} catch (Exception ex) when (!(ex is OperationCanceledException && searchCtx.CancellationToken.IsCancellationRequested)) {
				var logger = LoggerService.GetLogger<MSBuildReferenceCollector> (buffer);
				LogErrorFindReferences (logger, ex);
			}
			await searchCtx.OnCompletedAsync ();
		}

		async Task FindPropertyWrites (string propertyName, ITextBuffer buffer)
		{
			var title = MSBuildNavigationHelpers.GetFindPropertyWritesSearchTitle(propertyName);
			var searchCtx = Presenter.StartSearch (title, propertyName, true);

			try {
				await FindReferences (
					searchCtx,
					(doc, text, logger, reporter) => new MSBuildPropertyReferenceCollector (doc, text, logger, propertyName, reporter),
					buffer,
					MSBuildNavigationHelpers.FilterUsageWrites);
			} catch (Exception ex) when (!(ex is OperationCanceledException && searchCtx.CancellationToken.IsCancellationRequested)) {
				var logger = LoggerService.GetLogger<MSBuildReferenceCollector> (buffer);
				LogErrorFindReferences (logger, ex);
			}
			await searchCtx.OnCompletedAsync ();
		}

		async Task FindItemWrites (string itemName, ITextBuffer buffer)
		{
			var title = MSBuildNavigationHelpers.GetFindTargetDefinitionsSearchTitle (itemName);
			var searchCtx = Presenter.StartSearch (title, itemName, true);

			try {
				await FindReferences (
					searchCtx,
					(doc, text, logger, reporter) => new MSBuildItemReferenceCollector (doc, text, logger, itemName, reporter),
					buffer,
					MSBuildNavigationHelpers.FilterUsageWrites);
			} catch (Exception ex) when (!(ex is OperationCanceledException && searchCtx.CancellationToken.IsCancellationRequested)) {
				var logger = LoggerService.GetLogger<MSBuildReferenceCollector> (buffer);
				LogErrorFindReferences (logger, ex);
			}
			await searchCtx.OnCompletedAsync ();
		}

		/// <remarks>
		/// this does not need a cancellation token because it creates UI that handles cancellation
		/// </remarks>
		async Task FindReferences (
			FindReferencesContext searchCtx,
			MSBuildReferenceCollectorFactory collectorFactory,
			ITextBuffer buffer,
			Func<FindReferencesResult, bool>? resultFilter = null)
		{
			var openDocuments = EditorHost.GetOpenDocuments ();

			var msbuildContentType = ContentTypeRegistry.GetContentType (MSBuildContentType.Name);

			var parser = ParserProvider.GetParser (buffer);
			var r = await parser.GetOrProcessAsync (buffer.CurrentSnapshot, searchCtx.CancellationToken);
			var originDoc = r.MSBuildDocument;
			var logger = LoggerService.GetLogger<MSBuildReferenceCollector> (buffer);

			var jobs = originDoc.GetDescendentImports ()
				.Where (imp => imp.IsResolved)
				.Select (imp => new FindReferencesSearchJob (imp.Filename, null, null))
				.Prepend (new FindReferencesSearchJob (originDoc.Filename, originDoc.XDocument, originDoc.Text as SnapshotTextSource))
				.ToList ();

			int jobsCompleted = 0;

			await ParallelAsync.ForEach (jobs, Environment.ProcessorCount, async (job, token) => {
				try {
					if (job.TextSource == null) {
						if (!File.Exists (job.Filename)) {
							return;
						}
						var xmlParser = new XmlTreeParser (new XmlRootState ());
						if (!openDocuments.TryGetValue (job.Filename, out var buf)) {
							buf = BufferFactory.CreateTextBuffer (File.OpenText (job.Filename), msbuildContentType);
						}
						job.TextSource = buf.CurrentSnapshot.GetTextSource ();
						(job.Document, _) = xmlParser.Parse (job.TextSource.CreateReader (), token);
					}

					token.ThrowIfCancellationRequested ();

					// the collector only uses the MSBuildDocument to resolve schemas,
					// so we can use the root document here.
					var collector = collectorFactory (originDoc, job.TextSource, logger, ReportResult);
					collector.Run (job.Document.RootElement);

					var progress = Interlocked.Increment (ref jobsCompleted);
					await searchCtx.ReportProgressAsync (progress, jobs.Count);

					void ReportResult (FindReferencesResult result)
					{
						if (resultFilter is not null && resultFilter (result) == false) {
							return;
						}

						var line = job.TextSource.Snapshot.GetLineFromPosition (result.Offset);
						var col = result.Offset - line.Start.Position;
						var lineText = line.GetText ();
						var highlight = new TextSpan (col, result.Length);

						// FIXME syntax highlighting
						//for now, just slice this up as the highlight works on the span at the highlight offset
						var classifiedSpans = ImmutableArray<ClassifiedText>.Empty;
						classifiedSpans = classifiedSpans.Add (new ClassifiedText (lineText.Substring (0, highlight.Start), PredefinedClassificationTypeNames.NaturalLanguage));
						classifiedSpans = classifiedSpans.Add (new ClassifiedText (lineText.Substring (highlight.Start, highlight.Length), PredefinedClassificationTypeNames.NaturalLanguage));
						classifiedSpans = classifiedSpans.Add (new ClassifiedText (lineText.Substring (highlight.End), PredefinedClassificationTypeNames.NaturalLanguage));

						_ = searchCtx.OnReferenceFoundAsync (
							new FoundReference (
								job.Filename,
								line.LineNumber, col,
								result.Offset, result.Length,
								result.Usage,
								classifiedSpans,
								highlight));
					}

				} catch (Exception ex) {
					MSBuildNavigationHelpers.LogErrorSearchingFile (logger, ex, job.Filename);
				}
			}, searchCtx.CancellationToken);
		}

		[LoggerMessage (EventId = 0, Level = LogLevel.Error, Message = "Unhandled error in Find References'")]
		static partial void LogErrorFindReferences (ILogger logger, Exception ex);

		[LoggerMessage (EventId = 1, Level = LogLevel.Error, Message = "Unhandled error navigating to multiple files")]
		static partial void LogErrorShowingNavigateMultiple (ILogger logger, Exception ex);

		class FindReferencesSearchJob
		{
			public FindReferencesSearchJob (string filename, XDocument document, SnapshotTextSource textSource)
			{
				Filename = filename;
				Document = document;
				TextSource = textSource;
			}

			public string Filename { get; }
			public XDocument Document { get; set; }
			public SnapshotTextSource TextSource { get; set; }
		}

		// based on https://blogs.msdn.microsoft.com/pfxteam/2012/03/05/implementing-a-simple-foreachasync-part-2/
		// can be removed when https://github.com/dotnet/corefx/issues/34233 is fixed
		static class ParallelAsync
		{
			public static Task ForEach<T> (IEnumerable<T> source, int dop, Func<T, CancellationToken, Task> body, CancellationToken token)
			{
				return Task.WhenAll (
					from partition in System.Collections.Concurrent.Partitioner.Create (source).GetPartitions (dop)
					select Task.Run (async delegate {
						using (partition)
							while (partition.MoveNext ()) {
								token.ThrowIfCancellationRequested ();
								await body (partition.Current, token);
							}
					}));
			}
		}
	}
}
