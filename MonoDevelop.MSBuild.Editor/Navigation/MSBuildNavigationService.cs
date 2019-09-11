// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Threading;

using MonoDevelop.MSBuild.Editor.Completion;
using MonoDevelop.MSBuild.Editor.Host;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.PackageSearch;
using MonoDevelop.Xml.Editor.Completion;

using ProjectFileTools.NuGetSearch.Contracts;
using ProjectFileTools.NuGetSearch.Feeds;

namespace MonoDevelop.MSBuild.Editor.Navigation
{
	[Export, PartCreationPolicy (CreationPolicy.Shared)]
	class MSBuildNavigationService
	{
		[Import (typeof (IFunctionTypeProvider))]
		internal IFunctionTypeProvider FunctionTypeProvider { get; set; }

		[Import (typeof (IPackageSearchManager))]
		public IPackageSearchManager PackageSearchManager { get; set; }

		[Import (typeof (IMSBuildEditorHost))]
		public IMSBuildEditorHost EditorHost { get; set; }

		[Import]
		public JoinableTaskContext JoinableTaskContext { get; set; }

		public bool CanNavigate (ITextBuffer buffer, SnapshotPoint point) => CanNavigate (buffer, point, out _);

		public bool CanNavigate (ITextBuffer buffer, SnapshotPoint point, out MSBuildReferenceKind referenceKind)
		{
			AssertMainThread ();

			var parser = BackgroundParser<MSBuildParseResult>.GetParser<MSBuildBackgroundParser> ((ITextBuffer2)buffer);

			MSBuildRootDocument doc = parser.LastParseResult.MSBuildDocument;
			var rr = MSBuildResolver.Resolve (
				parser.GetSpineParser (point),
				point.Snapshot.GetTextSource (),
				doc, FunctionTypeProvider);

			if (MSBuildNavigation.CanNavigate (doc, point, rr)) {
				referenceKind = rr.ReferenceKind;
				return true;
			}

			referenceKind = MSBuildReferenceKind.None;
			return false;
		}

		void AssertMainThread ()
		{
			if (!JoinableTaskContext.IsOnMainThread) {
				throw new InvalidOperationException ("Currently only valid on main thread as spine parser cache is not thread safe");
			}
		}

		public MSBuildNavigationResult GetNavigationResult (ITextBuffer buffer, SnapshotPoint point)
		{
			var parser = BackgroundParser<MSBuildParseResult>.GetParser<MSBuildBackgroundParser> ((ITextBuffer2)buffer);

			MSBuildRootDocument doc = parser.LastParseResult.MSBuildDocument;
			var rr = MSBuildResolver.Resolve (
				parser.GetSpineParser (point),
				point.Snapshot.GetTextSource (),
				doc, FunctionTypeProvider);

			return MSBuildNavigation.GetNavigation (doc, point, rr);
		}

		public bool Navigate (ITextBuffer buffer, SnapshotPoint point)
		{
			var result = GetNavigationResult (buffer, point);
			if (result != null) {
				return Navigate (result);
			}
			return false;
		}

		public bool Navigate (MSBuildNavigationResult result)
		{
			if (result.Kind == MSBuildReferenceKind.Target) {
				//TODO: need ability to display multiple results
				//FindReferences (() => new MSBuildTargetDefinitionCollector (result.Name), doc);
				return true;
			}

			if (result.Paths != null) {
				EditorHost.ShowGoToDefinitionResults (result.Paths);
				return true;
			}

			if (result.DestFile != null) {
				EditorHost.OpenFile (result.DestFile, result.DestOffset);
				return true;
			}

			if (result.NuGetID != null) {
				OpenNuGetUrl (result.NuGetID, EditorHost);
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
	}
}
