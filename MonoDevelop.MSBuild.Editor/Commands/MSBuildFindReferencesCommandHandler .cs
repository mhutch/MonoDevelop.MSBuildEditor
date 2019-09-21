// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.TextFormatting;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Utilities;
using MonoDevelop.MSBuild.Editor.Completion;
using MonoDevelop.MSBuild.Editor.Host;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Editor.Completion;
using MonoDevelop.Xml.Parser;

namespace MonoDevelop.MSBuild.Editor.Commands
{
	[Export (typeof (ICommandHandler))]
	[ContentType (MSBuildContentType.Name)]
	[Name ("MSBuild Find References")]
	class MSBuildFindReferencesCommandHandler : ICommandHandler<FindReferencesCommandArgs>
	{
		[Import]
		MSBuildCachingResolver Resolver { get; }

		[Import]
		public IStreamingFindReferencesPresenter Presenter { get; set; }

		[Import]
		public ITextBufferFactoryService BufferFactory { get; set; }

		[Import]
		public IContentTypeRegistryService ContentTypeRegistry { get; set; }

		[Import]
		public IMSBuildEditorHost EditorHost { get; set; }

		public string DisplayName { get; } = "Find References";

		public bool ExecuteCommand (FindReferencesCommandArgs args, CommandExecutionContext executionContext)
		{
			var pos = args.TextView.Caret.Position.BufferPosition;
			bool docIsUpToDate = Resolver.GetResolvedReference (args.SubjectBuffer, pos, out var doc, out var rr);

			FindReferences (rr, doc, docIsUpToDate, args.SubjectBuffer);

			return true;
		}

		public CommandState GetCommandState (FindReferencesCommandArgs args)
		{
			var pos = args.TextView.Caret.Position.BufferPosition;
			Resolver.GetResolvedReference (args.SubjectBuffer, pos, out var _, out var rr);

			if (MSBuildReferenceCollector.CanCreate (rr)) {
				return CommandState.Available;
			}

			// visible but disabled
			return new CommandState (true, false, false, true);
		}

		void FindReferences (MSBuildResolveResult reference, MSBuildRootDocument doc, bool docIsUpToDate, ITextBuffer buffer)
		{
			var searchCtx = Presenter.StartSearch ("Find References", true);
			FindReferences (searchCtx, reference, doc, docIsUpToDate, buffer).ContinueWith (t => {
				if (t.IsFaulted) {
					LoggingService.LogError ($"Error in find references", t.Exception);
				}
			});
		}

		async Task FindReferences (FindReferencesContext searchCtx, MSBuildResolveResult reference, MSBuildRootDocument doc, bool docIsUpToDate, ITextBuffer buffer)
		{
			var openDocuments = EditorHost.GetOpenDocuments ();

			var msbuildContentType = ContentTypeRegistry.GetContentType (MSBuildContentType.Name);

			if (!docIsUpToDate) {
				var parser = BackgroundParser<MSBuildParseResult>.GetParser<MSBuildBackgroundParser> ((ITextBuffer2)buffer);
				await parser.GetOrParseAsync ((ITextSnapshot2)buffer.CurrentSnapshot, searchCtx.CancellationToken);
			}

			var jobs = doc.GetDescendentImports ()
				.Where (imp => imp.IsResolved)
				.Select (imp => new FindReferencesSearchJob (imp.Filename, null, null))
				.Prepend (new FindReferencesSearchJob (doc.Filename, doc.XDocument, doc.Text))
				.ToList ();

			int jobsCompleted = jobs.Count;

			await ParallelAsync.ForEach (jobs, Environment.ProcessorCount, async (job, token) => {
				try {
					if (job.TextSource == null) {
						if (!File.Exists (job.Filename)) {
							return;
						}
						var xmlParser = new XmlParser (new XmlRootState (), true);
						if (!openDocuments.TryGetValue (job.Filename, out var buf)) {
							buf = BufferFactory.CreateTextBuffer (File.OpenText (job.Filename), msbuildContentType);
						}
						var textSource = buf.CurrentSnapshot.GetTextSource (job.Filename);
						xmlParser.Parse (textSource.CreateReader ());
						job.Document = xmlParser.Nodes.GetRoot ();
					}

					token.ThrowIfCancellationRequested ();

					var collector = MSBuildReferenceCollector.Create (reference, Resolver.FunctionTypeProvider, ReportResult);
					collector.Run (job.Document, job.TextSource, doc);

					var progress = Interlocked.Increment (ref jobsCompleted);
					await searchCtx.ReportProgressAsync (progress, jobs.Count);

					void ReportResult ((int Offset, int Length, ReferenceUsage Usage) result)
					{
						_ = searchCtx.OnReferenceFoundAsync (new FoundReference (job.Filename, result.Offset, result.Length, result.Usage));
					}

				} catch (Exception ex) {
					//monitor.ReportError ($"Error searching file {Path.GetFileName (import.Filename)}", ex);
					LoggingService.LogError ($"Error searching MSBuild file {job.Filename}", ex);
				}
			}, searchCtx.CancellationToken);

			await searchCtx.OnCompletedAsync ();
		}

		class FindReferencesSearchJob
		{
			public FindReferencesSearchJob (string filename, XDocument document, ITextSource textSource)
			{
				Filename = filename;
				Document = document;
				TextSource = textSource;
			}

			public string Filename { get; }
			public XDocument Document { get; set; }
			public ITextSource TextSource { get; set; }
		}

		// based on https://blogs.msdn.microsoft.com/pfxteam/2012/03/05/implementing-a-simple-foreachasync-part-2/
		// cam be removed when https://github.com/dotnet/corefx/issues/34233 is fixed
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
