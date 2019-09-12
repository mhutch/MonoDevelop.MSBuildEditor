// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;

using MonoDevelop.MSBuild.Editor.Completion;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.PackageSearch;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Editor.Completion;

using ProjectFileTools.NuGetSearch.Contracts;
using ProjectFileTools.NuGetSearch.Feeds;

namespace MonoDevelop.MSBuild.Editor.QuickInfo
{
	class MSBuildQuickInfoSource : IAsyncQuickInfoSource
	{
		readonly ITextBuffer textBuffer;
		readonly MSBuildQuickInfoSourceProvider provider;

		public MSBuildQuickInfoSource (ITextBuffer textBuffer, MSBuildQuickInfoSourceProvider provider)
		{
			this.textBuffer = textBuffer;
			this.provider = provider;
		}

		protected MSBuildBackgroundParser GetParser () => BackgroundParser<MSBuildParseResult>.GetParser<MSBuildBackgroundParser> ((ITextBuffer2)textBuffer);

		public async Task<QuickInfoItem> GetQuickInfoItemAsync (IAsyncQuickInfoSession session, CancellationToken cancellationToken)
		{
			var parser = GetParser ();
			var snapshot = textBuffer.CurrentSnapshot;

			var result = await parser.GetOrParseAsync ((ITextSnapshot2)snapshot, cancellationToken);
			var doc = result.MSBuildDocument;
			//.LastParseResult?.MSBuildDocument ?? MSBuildRootDocument.CreateTestDocument ();

			if (doc == null) {
				return null;
			}

			var trigger = session.GetTriggerPoint (textBuffer);
			var offset = trigger.GetPosition (snapshot);

			var spine = parser.GetSpineParser (new SnapshotPoint (snapshot, offset));

			var annotations = MSBuildNavigation.GetAnnotationsAtOffset<NavigationAnnotation> (doc, offset)?.ToList ();
			if (annotations != null && annotations.Count > 0) {
				return CreateQuickInfo (snapshot, annotations);
			}

			//FIXME: can we avoid awaiting this unless we actually need to resolve a function? need to propagate async downwards
			await provider.FunctionTypeProvider.EnsureInitialized (cancellationToken);

			var rr = MSBuildResolver.Resolve (
				spine, snapshot.GetTextSource (), doc, provider.FunctionTypeProvider, cancellationToken
			);
			if (rr != null) {
				if (rr.ReferenceKind == MSBuildReferenceKind.NuGetID) {
					return await CreateNuGetQuickInfo (snapshot, doc, rr, cancellationToken);
				}
				var info = rr.GetResolvedReference (doc, provider.FunctionTypeProvider);
				if (info != null) {
					var element = await provider.DisplayElementFactory.GetInfoTooltipElement (doc, info, rr, cancellationToken);
					return new QuickInfoItem (
						snapshot.CreateTrackingSpan (rr.ReferenceOffset, rr.ReferenceLength, SpanTrackingMode.EdgeInclusive),
						element);
				}
			}
			return null;
		}

		QuickInfoItem CreateQuickInfo (ITextSnapshot snapshot, IEnumerable<NavigationAnnotation> annotations)
		{
			var navs = annotations.ToList ();

			var first = navs.First ();
			var span = snapshot.CreateTrackingSpan (first.Span.Start, first.Span.Length, SpanTrackingMode.EdgeInclusive);

			return new QuickInfoItem (span, provider.DisplayElementFactory.GetResolvedPathElement (navs));
		}

		//FIXME: can we display some kind of "loading" message while it loads?
		async Task<QuickInfoItem> CreateNuGetQuickInfo (ITextSnapshot snapshot, MSBuildRootDocument doc, MSBuildResolveResult rr, CancellationToken token)
		{
			IPackageInfo info = null;
			var packageId = (string)rr.Reference;

			try {
				var frameworkId = doc.GetTargetFrameworkNuGetSearchParameter ();

				//FIXME: can we use the correct version here?
				var infos = await provider.PackageSearchManager.SearchPackageInfo (packageId, null, frameworkId).ToTask (token);

				//prefer non-local results as they will have more metadata
				info = infos
					.FirstOrDefault (p => p.SourceKind != ProjectFileTools.NuGetSearch.Feeds.FeedKind.Local)
					?? infos.FirstOrDefault ();
			}
			catch (Exception ex) when (!(ex is OperationCanceledException && token.IsCancellationRequested)) {
				LoggingService.LogError ("Error loading package description", ex);
			}

			var span = snapshot.CreateTrackingSpan (rr.ReferenceOffset, rr.ReferenceLength, SpanTrackingMode.EdgeInclusive);
			return new QuickInfoItem (span, provider.DisplayElementFactory.GetPackageInfoTooltip (packageId, info, FeedKind.NuGet));
		}

		public void Dispose ()
		{
		}
	}
}
