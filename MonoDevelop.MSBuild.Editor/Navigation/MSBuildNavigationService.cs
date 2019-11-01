// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
using MonoDevelop.Xml.Parser;

using ProjectFileTools.NuGetSearch.Contracts;
using ProjectFileTools.NuGetSearch.Feeds;

namespace MonoDevelop.MSBuild.Editor.Navigation
{
	[Export, PartCreationPolicy (CreationPolicy.Shared)]
	class MSBuildNavigationService
	{
		[Import]
		public IPackageSearchManager PackageSearchManager { get; set; }

		[Import]
		public IMSBuildEditorHost EditorHost { get; set; }

		[Import]
		public JoinableTaskContext JoinableTaskContext { get; set; }

		[Import]
		public IStreamingFindReferencesPresenter Presenter { get; set; }

		[Import]
		public MSBuildCachingResolver Resolver { get; set; }

		[Import]
		public IContentTypeRegistryService ContentTypeRegistry { get; set; }

		[Import]
		public ITextBufferFactoryService BufferFactory { get; set; }

		[Import]
		public MSBuildParserProvider ParserProvider { get; set; }

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

		public bool Navigate (MSBuildNavigationResult result, ITextBuffer buffer)
		{
			if (result.Kind == MSBuildReferenceKind.Target) {
				FindTargetDefinitions (result.Name, buffer);
				return true;
			}

			if (result.Paths != null) {
				if (result.Paths.Length == 1) {
					EditorHost.OpenFile (result.Paths[0], 0);
					return true;
				}
				if (result.Paths.Length > 1) {
					ShowMultipleFiles (result.Paths);
					return true;
				}
			}

			if (result.DestFile != null) {
				EditorHost.OpenFile (result.DestFile, result.DestOffset);
				return true;
			}

			if (result.Kind == MSBuildReferenceKind.NuGetID) {
				OpenNuGetUrl (result.Name, EditorHost);
				return true;
			}

			return false;
		}

		void OpenNuGetUrl (string nuGetId, IMSBuildEditorHost host)
		{
			Task.Run (async () => {
				var results = await PackageSearchManager.SearchPackageInfo (nuGetId, null, null).ToTask ();

				if (results.Any (r => r.SourceKind == FeedKind.NuGet)) {
					var url = $"https://www.nuget.org/packages/{Uri.EscapeUriString (nuGetId)}";
					Process.Start (url);
				} else {
					await JoinableTaskContext.Factory.SwitchToMainThreadAsync ();
					host.ShowStatusBarMessage ("Package is not from NuGet.org");
				}
			});
		}

		async void ShowMultipleFiles (string[] files)
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
						LoggingService.LogError ($"Error getting text for file {file}", ex);
						continue;
					}
					var classifiedSpans = ImmutableArray<ClassifiedText>.Empty;
					classifiedSpans = classifiedSpans.Add (new ClassifiedText (lineText, PredefinedClassificationTypeNames.NaturalLanguage));
					await searchCtx.OnReferenceFoundAsync (new FoundReference (file, 0, 0, ReferenceUsage.Declaration, classifiedSpans, new TextSpan (-1, 0)));
				}
			} catch (Exception ex) when (!(ex is OperationCanceledException && searchCtx.CancellationToken.IsCancellationRequested)) {
				LoggingService.LogError ($"Error in show multiple imports", ex);
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
			FindReferencesAsync (buffer, resolveResult);
			return true;
		}

		async void FindReferencesAsync (ITextBuffer buffer, MSBuildResolveResult reference)
		{
			var referenceName = reference.GetReferenceName ();
			var searchCtx = Presenter.StartSearch ($"'{referenceName}' references", referenceName, true);
			try {
				await FindReferences (searchCtx, a => MSBuildReferenceCollector.Create (reference, Resolver.FunctionTypeProvider, a), buffer);
			} catch (Exception ex) when (!(ex is OperationCanceledException && searchCtx.CancellationToken.IsCancellationRequested)) {
				LoggingService.LogError ($"Error in find references", ex);
			}
			await searchCtx.OnCompletedAsync ();
		}

		async void FindTargetDefinitions (string targetName, ITextBuffer buffer)
		{
			var searchCtx = Presenter.StartSearch ($"'{targetName}' definitions", targetName, true);
			try {
				await FindReferences (searchCtx, (a) => new MSBuildTargetDefinitionCollector (targetName, a), buffer);
			} catch (Exception ex) when (!(ex is OperationCanceledException && searchCtx.CancellationToken.IsCancellationRequested)) {
				LoggingService.LogError ($"Error in find references", ex);
			}
			await searchCtx.OnCompletedAsync ();
		}

		delegate MSBuildReferenceCollector ReferenceCollectorFactory (Action<(int Offset, int Length, ReferenceUsage Usage)> reportResult);

		async Task FindReferences (
			FindReferencesContext searchCtx,
			ReferenceCollectorFactory collectorFactory,
			ITextBuffer buffer)
		{
			var openDocuments = EditorHost.GetOpenDocuments ();

			var msbuildContentType = ContentTypeRegistry.GetContentType (MSBuildContentType.Name);

			var parser = ParserProvider.GetParser (buffer);
			var r = await parser.GetOrProcessAsync (buffer.CurrentSnapshot, searchCtx.CancellationToken);
			var doc = r.MSBuildDocument;

			var jobs = doc.GetDescendentImports ()
				.Where (imp => imp.IsResolved)
				.Select (imp => new FindReferencesSearchJob (imp.Filename, null, null))
				.Prepend (new FindReferencesSearchJob (doc.Filename, doc.XDocument, doc.Text as SnapshotTextSource))
				.ToList ();

			int jobsCompleted = jobs.Count;

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
						(job.Document, _) = xmlParser.Parse (job.TextSource.CreateReader ());
					}

					token.ThrowIfCancellationRequested ();

					var collector = collectorFactory (ReportResult);
					collector.Run (job.Document, job.TextSource, doc);

					var progress = Interlocked.Increment (ref jobsCompleted);
					await searchCtx.ReportProgressAsync (progress, jobs.Count);

					void ReportResult ((int Offset, int Length, ReferenceUsage Usage) result)
					{
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

						_ = searchCtx.OnReferenceFoundAsync (new FoundReference (job.Filename, line.LineNumber, col, result.Usage, classifiedSpans, highlight));
					}

				} catch (Exception ex) {
					LoggingService.LogError ($"Error searching MSBuild file {job.Filename}", ex);
				}
			}, searchCtx.CancellationToken);
		}

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
