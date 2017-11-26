// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MonoDevelop.Core;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Editor;
using MonoDevelop.Ide.Editor.Extension;
using MonoDevelop.Ide.FindInFiles;
using MonoDevelop.MSBuildEditor.Language;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Parser;

namespace MonoDevelop.MSBuildEditor
{
	class MSBuildNavigationExtension : AbstractNavigationExtension
	{
		protected override Task<IEnumerable<NavigationSegment>> RequestLinksAsync (int offset, int length, CancellationToken token)
		{
			var doc = DocumentContext.ParsedDocument as MSBuildParsedDocument;
			if (doc == null) {
				return Task.FromResult (Enumerable.Empty<NavigationSegment> ());
			}
			return Task.Run (
				() => CreateNavigationSegments (
					doc,
					MSBuildNavigation.ResolveAll (doc.XDocument, doc.FileName, doc.Text, doc.Context)
				)
			);
		}

		static IEnumerable<NavigationSegment> CreateNavigationSegments (MSBuildParsedDocument doc, List<MSBuildNavigationResult> results)
		{
			foreach (var result in results) {
				yield return new NavigationSegment (result.Offset, result.Length, () => Navigate (result, doc));
			}
		}

		public static void Navigate (MSBuildNavigationResult result, MSBuildParsedDocument doc)
		{
			try {
				switch (result.Kind) {
				case MSBuildReferenceKind.None:
					NavigatePaths (result.Paths);
					break;
				case MSBuildReferenceKind.Target:
					FindReferences (() => new MSBuildTargetDefinitionCollector (result.Name), doc);
					break;
				}
			} catch (Exception ex) {
				LoggingService.LogError ("MSBuild navigation failed", ex);
			}
		}

		static void NavigatePaths (string [] paths)
		{
			var files = new List<string> ();
			foreach (var path in paths) {
				if (Directory.Exists (path)) {
					foreach (var f in Directory.EnumerateFiles (path, "*", SearchOption.AllDirectories)) {
						if (f.EndsWith (".targets", StringComparison.OrdinalIgnoreCase) || f.EndsWith (".props", StringComparison.OrdinalIgnoreCase))
							files.Add (f);
					}
				}
				if (File.Exists (path)) {
					files.Add (path);
				}
			}

			if (files.Count == 1) {
				//FIXME: can we open the doc with the same context i.e. as a child of this?
				// That would improve drilldown and find refs accuracy but would run into issues
				// when drilling down into the same child from multiple parents.
				// We'd probably need something like the shared projects context dropdown.
				IdeApp.Workbench.OpenDocument (files [0], null, true);
				return;
			}

			using (var monitor = IdeApp.Workbench.ProgressMonitors.GetSearchProgressMonitor (true, true)) {
				foreach (var file in files) {
					var fp = new FileProvider (file);
					monitor.ReportResult (new SearchResult (fp, 0, 0));
				}
			}
		}

		public static void FindReferences (Func<MSBuildReferenceCollector> createCollector, MSBuildParsedDocument doc)
		{
			var monitor = IdeApp.Workbench.ProgressMonitors.GetSearchProgressMonitor (true, true);

			var tasks = new List<Task> ();

			foreach (var import in doc.Context.GetDescendentImports ()) {
				if (!import.IsResolved || !File.Exists (import.Filename)) {
					continue;
				}
				tasks.Add (Task.Run (() => {
					try {
						var xmlParser = new XmlParser (new XmlRootState (), true);
						var textDoc = TextEditorFactory.CreateNewDocument (import.Filename, MSBuildTextEditorExtension.MSBuildMimeType);
						xmlParser.Parse (textDoc.CreateReader ());
						var xdoc = xmlParser.Nodes.GetRoot ();
						FindReferences (createCollector (), monitor, import.Filename, xdoc, textDoc, doc.Context);
					} catch (Exception ex) {
						monitor.ReportError ($"Error searching file {Path.GetFileName (import.Filename)}", ex);
						LoggingService.LogError ($"Error searching MSBuild file {import.Filename}", ex);
					}
				}));
			}

			tasks.Add (Task.Run (() => {
				try {
					var textDoc = TextEditorFactory.CreateNewDocument (doc.Text, doc.FileName, MSBuildTextEditorExtension.MSBuildMimeType);
					FindReferences (createCollector(), monitor, doc.FileName, doc.XDocument, textDoc, doc.Context);
				} catch (Exception ex) {
					monitor.ReportError ($"Error searching file {Path.GetFileName (doc.FileName)}", ex);
					LoggingService.LogError ($"Error searching MSBuild file {doc.FileName}", ex);
				}
			}));

			Task.WhenAll (tasks).ContinueWith (t => monitor?.Dispose ());
		}

		static void FindReferences (
			MSBuildReferenceCollector collector,
			SearchProgressMonitor monitor,
			string filename, XDocument doc, IReadonlyTextDocument textDoc, MSBuildResolveContext context)
		{
			collector.Run (doc, filename, textDoc, context);
			var fileProvider = new FileProvider (filename);
			if (collector.Results.Count > 0) {
				monitor.ReportResults (collector.Results.Select (r => new SearchResult (fileProvider, r.Offset, r.Length)));
			}
		}
	}
}
