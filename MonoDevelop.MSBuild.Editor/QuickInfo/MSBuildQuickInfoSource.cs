// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
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
using MonoDevelop.MSBuild.PackageSearch;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Editor.Completion;
using ProjectFileTools.NuGetSearch.Contracts;

namespace MonoDevelop.MSBuild.Editor.QuickInfo
{
	class MSBuildQuickInfoSource : IAsyncQuickInfoSource
	{
		readonly ITextBuffer textBuffer;
		readonly IPackageSearchManager packageSearchManager;

		public MSBuildQuickInfoSource (ITextBuffer textBuffer, IPackageSearchManager packageSearchManager)
		{
			this.textBuffer = textBuffer;
			this.packageSearchManager = packageSearchManager;
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
					return await CreateNuGetQuickInfo (snapshot, doc, rr, cancellationToken);
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

		static QuickInfoItem CreateQuickInfo (ITextSnapshot snapshot, IEnumerable<NavigationAnnotation> annotations)
		{
			var navs = annotations.ToList ();

			var first = navs.First ();
			var span = snapshot.CreateTrackingSpan (first.Span.Start, first.Span.Length, SpanTrackingMode.EdgeInclusive);

			return new QuickInfoItem (span, DisplayElementFactory.GetResolvedPathElement (navs));
		}

		//FIXME: can we display some kind of "loading" message while it loads?
		async Task<QuickInfoItem> CreateNuGetQuickInfo (ITextSnapshot snapshot, MSBuildRootDocument doc, MSBuildResolveResult rr, CancellationToken token)
		{
			IPackageInfo info = null;

			try {
				info = (await PackageSearchHelpers.SearchPackageInfo (
					packageSearchManager, (string)rr.Reference, null, doc.GetTargetFrameworkNuGetSearchParameter (), CancellationToken.None
				)).FirstOrDefault ();
			}
			catch (Exception ex) {
				LoggingService.LogError ("Error loading package description", ex);
			}

			var span = snapshot.CreateTrackingSpan (rr.ReferenceOffset, rr.ReferenceLength, SpanTrackingMode.EdgeInclusive);
			return new QuickInfoItem (span, CreatePackageInfoElement ((string)rr.Reference, info));
		}

		static ContainerElement CreatePackageInfoElement (string id, IPackageInfo package)
		{
			var nameEl = new ClassifiedTextElement (
				new ClassifiedTextRun (PredefinedClassificationTypeNames.Keyword, "package"),
				new ClassifiedTextRun (PredefinedClassificationTypeNames.WhiteSpace, " "),
				new ClassifiedTextRun (PredefinedClassificationTypeNames.Type, package?.Id ?? id)
			);

			ClassifiedTextElement descEl;
			if (package != null) {
				var description = !string.IsNullOrWhiteSpace (package.Description) ? package.Description : package.Summary;
				if (string.IsNullOrWhiteSpace (description)) {
					description = package.Summary;
				}
				if (!string.IsNullOrWhiteSpace (description)) {
					descEl = new ClassifiedTextElement (
						new ClassifiedTextRun (PredefinedClassificationTypeNames.NaturalLanguage, description)
					);
				} else {
					descEl = new ClassifiedTextElement (
						new ClassifiedTextRun (PredefinedClassificationTypeNames.Comment, "[no description]")
					);
				}
			} else {
				descEl = new ClassifiedTextElement (
					new ClassifiedTextRun (PredefinedClassificationTypeNames.Comment, "Could not load package information")
				);
			}

			return new ContainerElement (
				ContainerElementStyle.Stacked | ContainerElementStyle.VerticalPadding,
				new ContainerElement (
					ContainerElementStyle.Wrapped,
					DisplayElementFactory.GetImageElement (KnownImages.NuGet),
					nameEl
				),
				descEl
			);
		}

		public void Dispose ()
		{
		}
	}
}
