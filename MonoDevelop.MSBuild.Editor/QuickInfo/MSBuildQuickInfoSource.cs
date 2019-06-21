// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;

using MonoDevelop.MSBuild.Editor.Completion;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Editor.Completion;

namespace MonoDevelop.MSBuild.Editor.QuickInfo
{
	class MSBuildQuickInfoSource : IAsyncQuickInfoSource
	{
		readonly ITextBuffer textBuffer;

		public MSBuildQuickInfoSource (ITextBuffer textBuffer)
		{
			this.textBuffer = textBuffer;
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

			var rr = MSBuildResolver.Resolve (spine, snapshot.GetTextSource (), doc);
			if (rr != null) {
				if (rr.ReferenceKind == MSBuildReferenceKind.NuGetID) {
					return CreateNuGetQuickInfo (snapshot, doc, rr, cancellationToken);
				}
				var info = rr.GetResolvedReference (doc);
				if (info != null) {
					return CreateQuickInfo (snapshot, doc, info, rr);
				}
			}
			return null;
		}

		static QuickInfoItem CreateQuickInfo (ITextSnapshot snapshot, MSBuildRootDocument doc, BaseInfo info, MSBuildResolveResult rr)
			=> new QuickInfoItem (
				snapshot.CreateTrackingSpan (rr.ReferenceOffset, rr.ReferenceLength, SpanTrackingMode.EdgeInclusive),
				DisplayElementFactory.GetInfoTooltipElement (doc, info, rr));

		QuickInfoItem CreateNuGetQuickInfo (ITextSnapshot snapshot, MSBuildRootDocument doc, MSBuildResolveResult rr, CancellationToken token)
		{
			//TODO nuget tooltips
			/*
			var packages = PackageSearchHelpers.SearchPackageInfo (
				ext.PackageSearchManager, (string)rr.Reference, null, doc.GetTargetFrameworkNuGetSearchParameter (), CancellationToken.None
			);

			var item = new InfoItem {
				Doc = doc,
				ResolveResult = rr,
			};
			return Task.FromResult (new TooltipItem (item, rr.ReferenceOffset, rr.ReferenceLength));
			*/
			return null;
		}

		static QuickInfoItem CreateQuickInfo (ITextSnapshot snapshot, IEnumerable<NavigationAnnotation> annotations)
		{
			var navs = annotations.ToList ();

			var first = navs.First ();
			var span = snapshot.CreateTrackingSpan (first.Span.Start, first.Span.Length, SpanTrackingMode.EdgeInclusive);

			return new QuickInfoItem (span, DisplayElementFactory.GetResolvedPathElement (navs));
		}

		public void Dispose ()
		{
		}
	}
}
