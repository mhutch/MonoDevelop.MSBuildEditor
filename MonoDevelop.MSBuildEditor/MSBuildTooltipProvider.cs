// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MonoDevelop.Components;
using MonoDevelop.Ide.CodeCompletion;
using MonoDevelop.Ide.Editor;
using MonoDevelop.MSBuildEditor.Language;
using MonoDevelop.MSBuildEditor.PackageSearch;
using MonoDevelop.MSBuildEditor.Schema;
using ProjectFileTools.NuGetSearch.Contracts;

namespace MonoDevelop.MSBuildEditor
{
	class MSBuildTooltipProvider : TooltipProvider
	{
		public override Task<TooltipItem> GetItem(TextEditor editor, DocumentContext ctx, int offset, CancellationToken token = default(CancellationToken))
        {
			var ext = editor.GetContent<MSBuildTextEditorExtension> ();
			MSBuildRootDocument doc;
			if (ext == null || (doc = ext.GetDocument ()) == null) {
				return Task.FromResult<TooltipItem> (null);
			}

			var loc = editor.OffsetToLocation (offset);
			var annotations = MSBuildNavigation.GetAnnotationsAtLocation<NavigationAnnotation> (doc, loc);
			if (annotations != null && annotations.Any ()) {
				var first = annotations.First ();
				int start = editor.LocationToOffset (first.Region.Begin);
				int end = editor.LocationToOffset (first.Region.End);
				return Task.FromResult (new TooltipItem (annotations, start, end - start));
			}

			var rr = ext.ResolveAt (offset);
			if (rr != null) {
				if (rr.ReferenceKind == MSBuildReferenceKind.NuGetID) {
					var item = new InfoItem {
						Doc = doc,
						ResolveResult = rr,
						Packages = PackageSearchHelpers.SearchPackageInfo (
							ext.PackageSearchManager, (string)rr.Reference, null, doc.GetTargetFramework (), CancellationToken.None
						)
					};
					return Task.FromResult (new TooltipItem (item, rr.ReferenceOffset, rr.ReferenceLength));
				}
				var info = rr.GetResolvedReference (doc);
				if (info != null) {
					var item = new InfoItem { Info = info, Doc = doc, ResolveResult = rr };
					return Task.FromResult (new TooltipItem (item, rr.ReferenceOffset, rr.ReferenceLength));
				}
			}
			return Task.FromResult<TooltipItem> (null);
        }

		public static TooltipInformation CreateTooltipInformation (MSBuildRootDocument doc, BaseInfo info, MSBuildResolveResult rr)
		{
			var formatter = new DescriptionMarkupFormatter (doc);
			var nameMarkup = formatter.GetNameMarkup (info);
			if (nameMarkup == null) {
				return null;
			}

			var desc = DescriptionFormatter.GetDescription (info, doc, rr);

			return new TooltipInformation {
				SignatureMarkup = nameMarkup,
				SummaryMarkup = GLib.Markup.EscapeText (desc),
				FooterMarkup = formatter.GetSeenInMarkup (info)
			};
		}

		public override Window CreateTooltipWindow (TextEditor editor, DocumentContext ctx, TooltipItem item, int offset, Xwt.ModifierKeys modifierState)
		{
			if (item.Item is InfoItem infoItem) {
				if (infoItem.Packages != null) {
					return CreatePackageWindow (infoItem);
				}
				return CreateItemWindow (infoItem);
			}

			if (item.Item is IEnumerable<NavigationAnnotation> annotations) {
				var navs = annotations.ToList ();
				var markup = DescriptionMarkupFormatter.GetNavigationMarkup (navs);
				return new LabelTooltipWindow (markup);
			}

			return null;
		}

		static Window CreateItemWindow (InfoItem infoItem)
		{
			TooltipInformation ti = null;
			ti = CreateTooltipInformation (infoItem.Doc, infoItem.Info, infoItem.ResolveResult);
			if (ti == null) {
				return null;
			}

			var window = new TooltipInformationWindow ();
			window.AddOverload (ti);
			window.ShowArrow = true;
			window.RepositionWindow ();
			return window;
		}

		static Window CreatePackageWindow (InfoItem infoItem)
		{
			var window = new TooltipInformationWindow ();
			window.ShowArrow = true;
			window.RepositionWindow ();

			var cts = new CancellationTokenSource ();
			window.Closed += delegate { cts.Cancel (); };

			var packages = infoItem.Packages;
			TooltipInformation ti;
			bool done = CreatePackageTooltipInfo ((string)infoItem.ResolveResult.Reference, packages, out ti);
			if (!done) {
				packages.ContinueWith (t => {
					if (!done) {
						done = CreatePackageTooltipInfo ((string)infoItem.ResolveResult.Reference, packages, out ti);
						if (ti != null) {
							window.Clear ();
							window.AddOverload (ti);
						}
					}
				}, cts.Token, TaskContinuationOptions.None, TaskScheduler.FromCurrentSynchronizationContext ());
			}

			window.AddOverload (ti);
			return window;
		}

		static bool CreatePackageTooltipInfo (string name, Task<IReadOnlyList<IPackageInfo>> info, out TooltipInformation ti) 
		{
			switch (info.Status) {
			case TaskStatus.Faulted:
				ti = new TooltipInformation {
					SignatureMarkup = $"{name}",
					SummaryMarkup = "<span color='#ff0000'><i>Could not load package information</i></span>"
				};
				return true;
			case TaskStatus.RanToCompletion:
				ti = PackageSearchHelpers.CreateTooltipInformation (info.Result);
				return true;
			case TaskStatus.Canceled:
				ti = null;
				return true;
			default:
				ti = new TooltipInformation {
					SignatureMarkup = $"{name}",
					SummaryMarkup = "<i>Loading...</i>"
				};
				return false;
			}
		}

		public override void GetRequiredPosition (TextEditor editor, Window tipWindow, out int requiredWidth, out double xalign)
		{
			if ((Gtk.Window)tipWindow is LabelTooltipWindow labelWin) {
				requiredWidth = labelWin.SetMaxWidth ((int)(labelWin.Screen.Width * 0.4));
				xalign = 0.5;
			} else {
				base.GetRequiredPosition (editor, tipWindow, out requiredWidth, out xalign);
			}
		}

		class InfoItem
		{
			public BaseInfo Info;
			public MSBuildResolveResult ResolveResult;
			public MSBuildRootDocument Doc;
			public Task<IReadOnlyList<IPackageInfo>> Packages;
		}
    }
}
