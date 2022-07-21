// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using MonoDevelop.Core;
using MonoDevelop.Core.Instrumentation;
using MonoDevelop.Ide;
using MonoDevelop.Ide.FindInFiles;
using MonoDevelop.MSBuild.Editor.Host;
using MonoDevelop.MSBuild.Language;

namespace MonoDevelop.MSBuildEditor
{
	[Export (typeof (IStreamingFindReferencesPresenter)), Shared]
	class StreamingFindUsagesPresenter : IStreamingFindReferencesPresenter
	{
		public void ClearAll ()
		{
		}

		public FindReferencesContext StartSearch (string title, string referenceName, bool showUsage)
		{
			return new MonoDevelopFindUsagesContext ();
		}
	}

	// this is borrowed from MonoDevelop.Refactoring/MonoDevelop.Refactoring/StreamingFindUsagesPresenter.cs
	// as of 8881a5a04f2c414296cd4b4a57ec562c7ecf19a4
	sealed partial class MonoDevelopFindUsagesContext : FindReferencesContext
	{
		readonly ConcurrentDictionary<SearchResult, object> antiDuplicatesSet
			= new ConcurrentDictionary<SearchResult, object> (new SearchResultComparer ());
		SearchProgressMonitor monitor;
		int reportedProgress = 0;
		ITimeTracker timer = null;

		public MonoDevelopFindUsagesContext ()
		{
			monitor = IdeApp.Workbench.ProgressMonitors.GetSearchProgressMonitor (true, true);
			monitor.BeginTask (GettextCatalog.GetString ("Searching..."), 100);
			CancellationToken = monitor.CancellationToken;
			CancellationToken.Register (Finished);
		}

		public override CancellationToken CancellationToken { get; }

		void Finished ()
		{
			if (!CancellationToken.IsCancellationRequested) {
				monitor?.ReportResults (antiDuplicatesSet.Keys);
			}
			monitor?.Dispose ();
			monitor = null;

			timer?.Dispose ();
			timer = null;
		}

		public override Task ReportMessageAsync (string message)
		{
			return base.ReportMessageAsync (message);
		}

		public override Task ReportProgressAsync (int current, int maximum)
		{
			int newProgress = current * 100 / maximum;
			monitor?.Step (newProgress - reportedProgress);
			return Task.CompletedTask;
		}

		public override Task OnReferenceFoundAsync (FoundReference reference)
		{
			//FIXME: as of 17.4, VSMac can no longer differentiate between usage (read/write) in search results
			var sr = SearchResult.Create(reference.FilePath, reference.Offset, reference.Length);

			antiDuplicatesSet.TryAdd (sr, null);

			return Task.CompletedTask;
		}

		public override Task OnCompletedAsync ()
		{
			Finished ();
			return Task.CompletedTask;
		}

		class SearchResultComparer : IEqualityComparer<SearchResult>
		{
			public bool Equals (SearchResult x, SearchResult y)
			{
				return x.FileName == y.FileName &&
							x.Offset == y.Offset &&
							x.Length == y.Length;
			}

			public int GetHashCode (SearchResult obj)
			{
				int hash = 17;
				hash = hash * 23 + obj.Offset.GetHashCode ();
				hash = hash * 23 + obj.Length.GetHashCode ();
				hash = hash * 23 + (obj.FileName ?? "").GetHashCode ();
				return hash;
			}
		}
	}
}