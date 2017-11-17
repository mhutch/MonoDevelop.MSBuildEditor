// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MonoDevelop.Components;
using MonoDevelop.Core;
using MonoDevelop.Ide.CodeCompletion;
using MonoDevelop.Ide.Editor;
using MonoDevelop.Ide.Editor.Highlighting;
using MonoDevelop.MSBuildEditor.Language;
using MonoDevelop.MSBuildEditor.Schema;

namespace MonoDevelop.MSBuildEditor
{
	public class MSBuildTooltipProvider : TooltipProvider
	{
        public override Task<TooltipItem> GetItem(TextEditor editor, DocumentContext ctx, int offset, CancellationToken token = default(CancellationToken))
        {
			var ext = editor.GetContent<MSBuildTextEditorExtension> ();
			if (ext == null) {
				return Task.FromResult<TooltipItem> (null);
			}

			var annotations = ext.GetAnnotationsAtLocation<NavigationAnnotation> (editor.CaretLocation);
			if (annotations != null && annotations.Any ()) {
				var first = annotations.First ();
				int start = editor.LocationToOffset (first.Region.Begin);
				int end = editor.LocationToOffset (first.Region.End);
				return Task.FromResult (new TooltipItem (annotations, start, end - start));
			}

			var rr = ext.ResolveAt (offset);
			if (rr != null) {
				var msbuildCtx = ext.GetDocument ().Context;
				var info = rr.GetResolvedInfo (msbuildCtx.GetSchemas ());
				if (info != null) {
					var item = new InfoItem { Info = info, Context = msbuildCtx, ResultResult = rr };
					return Task.FromResult (new TooltipItem (item, rr.ReferenceOffset, rr.ReferenceName.Length));
				}
			}
			return Task.FromResult<TooltipItem> (null);
        }

		public override Window CreateTooltipWindow (TextEditor editor, DocumentContext ctx, TooltipItem item, int offset, Xwt.ModifierKeys modifierState)
		{
			if (item.Item is InfoItem infoItem) {
				var nameMarkup = GetNameMarkup (infoItem);
				if (nameMarkup == null) {
					return null;
				}

				var desc = DescriptionFormatter.GetDescription (infoItem.Info, infoItem.Context, infoItem.ResultResult);

				var window = new TooltipInformationWindow ();
				var ti = new TooltipInformation {
					SignatureMarkup = nameMarkup,
					SummaryMarkup = GLib.Markup.EscapeText (desc),
					FooterMarkup = MSBuildCompletionData.AppendSeenIn (infoItem.Context, infoItem.Info, null)
				};
				window.AddOverload (ti);
				window.ShowArrow = true;
				window.RepositionWindow ();
				return window;
			}

			if (item.Item is IEnumerable<NavigationAnnotation> annotations) {
				var window = new TooltipPopoverWindow ();
				var sb = new StringBuilder ();
				int i = 0;
				foreach (var location in annotations) {
					sb.AppendLine (location.Path);
					if (++i == 5) {
						sb.AppendLine ("...");
						break;
					}
				}
				window.Text = sb.ToString ();
				window.ShowArrow = false;
				return window;
			}

			return null;
		}

		static string GetNameMarkup (InfoItem info)
		{
			var theme = GetColorTheme ();
			var color = SyntaxHighlightingService.GetColorFromScope (theme, "entity.name.tag.xml", EditorThemeColors.Foreground);
			var label = DescriptionFormatter.GetTitle (info.Info, info.ResultResult);
			if (label.kind == null) {
				return null;
			}
			return $"{label.kind} <span foreground=\"{color.ToPangoString ()}\">{GLib.Markup.EscapeText (label.name)}</span>";
		}

		static EditorTheme GetColorTheme ()
		{
			try {
				var theme = SyntaxHighlightingService.GetEditorTheme (Ide.IdeApp.Preferences.ColorScheme);
				if (theme.FitsIdeTheme (Ide.IdeApp.Preferences.UserInterfaceTheme))
					return theme;
				return Ide.IdeApp.Preferences.UserInterfaceTheme.GetDefaultColorStyle ();
			} catch (Exception e) {
				LoggingService.LogError ("Error getting color theme", e);
				return SyntaxHighlightingService.DefaultColorStyle;
			}
		}

		class InfoItem
		{
			public BaseInfo Info;
			public MSBuildResolveResult ResultResult;
			public MSBuildResolveContext Context;
		}
    }
}
