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
using MonoDevelop.Xml.Editor.IntelliSense;

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
					return CreateNuGetQuickInfo (snapshot, doc, rr);
				}
				var info = rr.GetResolvedReference (doc);
				if (info != null) {
					return CreateQuickInfo (snapshot, doc, info, rr);
				}
			}
			return null;
		}

		QuickInfoItem CreateQuickInfo (ITextSnapshot snapshot, MSBuildRootDocument doc, BaseInfo info, MSBuildResolveResult rr)
		{
			var span = snapshot.CreateTrackingSpan (rr.ReferenceOffset, rr.ReferenceLength, SpanTrackingMode.EdgeInclusive);

			/*
			var formatter = new DescriptionMarkupFormatter (doc);
			var nameMarkup = formatter.GetNameMarkup (info);
			if (nameMarkup.IsEmpty) {
				return null;
			}
			*/

			var desc = DescriptionFormatter.GetDescription (info, doc, rr);

			//TODO: format elements
			/*
			return new TooltipInformation {
				SignatureMarkup = nameMarkup.AsMarkup (),
				SummaryMarkup = desc.AsMarkup (),
				FooterMarkup = formatter.GetSeenInMarkup (info).AsMarkup ()
			};
			*/

			var content = new ContainerElement (
				ContainerElementStyle.Wrapped,
				new ClassifiedTextElement (
					new ClassifiedTextRun (PredefinedClassificationTypeNames.NaturalLanguage, desc.AsText ())
				)
			);


			return new QuickInfoItem (span, content);
		}

		QuickInfoItem CreateNuGetQuickInfo (ITextSnapshot snapshot, MSBuildRootDocument doc, MSBuildResolveResult rr)
		{
			//TODO nuget tooltips
			/*
			var item = new InfoItem {
				Doc = doc,
				ResolveResult = rr,
				Packages = PackageSearchHelpers.SearchPackageInfo (
		ext.PackageSearchManager, (string)rr.Reference, null, doc.GetTargetFrameworkNuGetSearchParameter (), CancellationToken.None
	)
			};
			return Task.FromResult (new TooltipItem (item, rr.ReferenceOffset, rr.ReferenceLength));
			*/
			return null; ;
		}

		QuickInfoItem CreateQuickInfo (ITextSnapshot snapshot, IEnumerable<NavigationAnnotation> annotations)
		{
			var navs = annotations.ToList ();

			var first = navs.First ();
			var span = snapshot.CreateTrackingSpan (first.Span.Start, first.Span.Length, SpanTrackingMode.EdgeInclusive);

			//TODO: format elements
			//var markup = DescriptionMarkupFormatter.GetNavigationMarkup (navs);

			var content = new ContainerElement (
				ContainerElementStyle.Wrapped,
				new ClassifiedTextElement (
					new ClassifiedTextRun (PredefinedClassificationTypeNames.NaturalLanguage, navs.First ().Path)
				)
			);

			return new QuickInfoItem (span, content);
		}

		public void Dispose ()
		{
		}
	}
}
