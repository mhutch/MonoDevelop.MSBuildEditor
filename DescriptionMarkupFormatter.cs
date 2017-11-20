// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using MonoDevelop.Core;
using MonoDevelop.Ide.Editor.Highlighting;
using MonoDevelop.MSBuildEditor.Language;
using MonoDevelop.MSBuildEditor.Schema;

namespace MonoDevelop.MSBuildEditor
{
	class DescriptionMarkupFormatter
	{
		const string varColorID = "entity.name.tag.xml";
		const string keywordColorId = "keyword";

		EditorTheme theme;
		MSBuildResolveContext ctx;
		IRuntimeInformation runtimeInfo;

		public DescriptionMarkupFormatter (MSBuildResolveContext ctx, IRuntimeInformation runtimeInfo)
		{
			theme = GetColorTheme();
			this.ctx = ctx;
			this.runtimeInfo = runtimeInfo;
		}

		string GetColor (string id)
		{
			var color = SyntaxHighlightingService.GetColorFromScope (theme, id, EditorThemeColors.Foreground);
			return color.ToPangoString ();
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

		public string GetNameMarkup (BaseInfo info)
		{
			var color = GetColor (keywordColorId);
			var label = DescriptionFormatter.GetTitle (info);
			if (label.kind == null) {
				return null;
			}
			return $"{label.kind} <span foreground=\"{color}\">{Escape (label.name)}</span>";
		}

		public string GetSeenInMarkup (BaseInfo info)
		{
			return AppendSeenInMarkup(info, null);
		}

		public string AppendSeenInMarkup (BaseInfo info, string baseDesc)
		{
			IEnumerable<string> seenIn = ctx.GetFilesSeenIn (info);
			StringBuilder sb = null;

			List<(string prefix, string subst)> prefixes = null;

			int count = 0;
			foreach (var s in seenIn) {
				if (count++ == 0) {
					sb = new StringBuilder ();
					prefixes = GetPrefixes (runtimeInfo);
					if (!string.IsNullOrEmpty (baseDesc)) {
						sb.AppendLine (baseDesc);
						sb.AppendLine ();
					}
					sb.Append ("<i>Seen in:</i>");
				}
				sb.AppendLine ();
				sb.Append ("  ");

				if (count == 4) {
					sb.Append ("...");
					break;
				}

				//factor out some common prefixes into variables
				//we do this instead of using the original string, as the result is simpler
				//and easier to understand
				var replacement = GetLongestReplacement (s, prefixes);
				if (!replacement.HasValue) {
					sb.Append (Escape (s));
					continue;
				}
				sb.Append ($"<span foreground=\"{GetColor(varColorID)}\">{Escape (replacement.Value.subst)}</span>");
				sb.Append (Escape (s.Substring (replacement.Value.prefix.Length)));
			}
			return sb?.ToString () ?? baseDesc;
		}

		static string Escape (string s) => GLib.Markup.EscapeText (s);

		static List<(string prefix, string subst)> GetPrefixes (IRuntimeInformation runtimeInfo)
		{
			var list = new List<(string prefix, string subst)> ();
			list.Add ((runtimeInfo.GetBinPath (), "$(MSBuildBinPath)"));
			list.Add ((runtimeInfo.GetToolsPath (), "$(MSBuildToolsPath)"));
			foreach (var extPath in runtimeInfo.GetExtensionsPaths ()) {
				list.Add ((extPath, "$(MSBuildExtensionsPath)"));
			}
			var sdksPath = runtimeInfo.GetSdksPath ();
			if (sdksPath != null) {
				list.Add ((sdksPath, "$(MSBuildSDKsPath)"));
			}
			return list;
		}

		static (string prefix, string subst)? GetLongestReplacement (string val, List<(string prefix, string subst)> replacements)
		{
			(string prefix, string subst)? longestReplacement = null;
			foreach (var replacement in replacements) {
				if (val.StartsWith (replacement.prefix, System.StringComparison.OrdinalIgnoreCase)) {
					if (!longestReplacement.HasValue || longestReplacement.Value.prefix.Length < replacement.prefix.Length) {
						longestReplacement = replacement;
					}
				}
			}
			return longestReplacement;
		}
	}
}
