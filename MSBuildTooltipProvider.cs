// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MonoDevelop.Components;
using MonoDevelop.Ide.CodeCompletion;
using MonoDevelop.Ide.Editor;
using MonoDevelop.MSBuildEditor.Language;
using MonoDevelop.MSBuildEditor.Schema;

namespace MonoDevelop.MSBuildEditor
{
	class MSBuildTooltipProvider : TooltipProvider
	{
        public override Task<TooltipItem> GetItem(TextEditor editor, DocumentContext ctx, int offset, CancellationToken token = default(CancellationToken))
        {
			var ext = editor.GetContent<MSBuildTextEditorExtension> ();
			MSBuildParsedDocument doc;
			if (ext == null || (doc = ext.GetDocument ()) == null) {
				return Task.FromResult<TooltipItem> (null);
			}

			var loc = editor.OffsetToLocation (offset);
			var annotations = ext.GetAnnotationsAtLocation<NavigationAnnotation> (loc);
			if (annotations != null && annotations.Any ()) {
				var first = annotations.First ();
				int start = editor.LocationToOffset (first.Region.Begin);
				int end = editor.LocationToOffset (first.Region.End);
				return Task.FromResult (new TooltipItem (annotations, start, end - start));
			}

			var rr = ext.ResolveAt (offset);
			if (rr != null) {
				var info = rr.GetResolvedReference (doc.Context.GetSchemas ());
				if (info != null) {
					var item = new InfoItem { Info = info, Doc = doc, ResolveResult = rr };
					return Task.FromResult (new TooltipItem (item, rr.ReferenceOffset, rr.ReferenceName.Length));
				}
			}
			return Task.FromResult<TooltipItem> (null);
        }

		public static TooltipInformation CreateTooltipInformation (MSBuildParsedDocument doc, BaseInfo info, MSBuildResolveResult rr)
		{
			var formatter = new DescriptionMarkupFormatter (doc.Context, doc.RuntimeInformation);
			var nameMarkup = formatter.GetNameMarkup (info);
			if (nameMarkup == null) {
				return null;
			}

			var desc = DescriptionFormatter.GetDescription (info, doc.Context, rr);

			return new TooltipInformation {
				SignatureMarkup = nameMarkup,
				SummaryMarkup = GLib.Markup.EscapeText (desc),
				FooterMarkup = formatter.GetSeenInMarkup (info)
			};
		}

		public override Window CreateTooltipWindow (TextEditor editor, DocumentContext ctx, TooltipItem item, int offset, Xwt.ModifierKeys modifierState)
		{
			if (item.Item is InfoItem infoItem) {
				var ti = CreateTooltipInformation (infoItem.Doc, infoItem.Info, infoItem.ResolveResult);
				if (ti == null) {
					return null;
				}

				var window = new TooltipInformationWindow ();
				window.AddOverload (ti);
				window.ShowArrow = true;
				window.RepositionWindow ();
				return window;
			}

			if (item.Item is IEnumerable<NavigationAnnotation> annotations) {
				var sb = new StringBuilder ();
				int i = 0;
				foreach (var location in annotations) {
					if (++i > 1) {
						sb.AppendLine ();
					}
					sb.Append (location.Path);
					if (i == 5) {
						sb.AppendLine ("...");
						break;
					}
				}
				return new LabelTooltipWindow (GLib.Markup.EscapeText (sb.ToString ()));
			}

			return null;
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
			public MSBuildParsedDocument Doc;
		}
    }
}
