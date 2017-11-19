// Copyright (c) 2014 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Text;
using MonoDevelop.MSBuildEditor.Language;
using MonoDevelop.MSBuildEditor.Schema;
using MonoDevelop.Xml.Completion;

namespace MonoDevelop.MSBuildEditor
{
	class MSBuildCompletionData : XmlCompletionData
	{
		readonly MSBuildParsedDocument doc;
		readonly MSBuildResolveResult rr;
		readonly BaseInfo info;
		string description;

		public MSBuildCompletionData (BaseInfo info, MSBuildParsedDocument doc, MSBuildResolveResult rr, DataType type)
			: base (info.Name, info.Description, type)
		{
			this.info = info;
			this.doc = doc;
			this.rr = rr;
		}

		public override string Description {
			get {
				return description ?? (description = GetDescription () ?? "");
			}
		}

		string GetDescription ()
		{
			var desc = DescriptionFormatter.GetDescription (info, doc.Context, rr);
			return AppendSeenInMarkup (doc.Context, doc.RuntimeInformation, info, desc, MSBuildTooltipProvider.GetColor ("entity.name.tag.xml"));
		}

		internal static string AppendSeenInMarkup (MSBuildResolveContext ctx, IRuntimeInformation runtimeInfo, BaseInfo info, string baseDesc, string varColor)
		{
			if (ctx == null) {
				return baseDesc;
			}

			IEnumerable<string> seenIn = ctx.GetFilesSeenIn (info);
			StringBuilder sb = null;

			List<(string prefix, string subst)> prefixes = null;

			foreach (var s in seenIn) {
				if (sb == null) {
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

				//factor out some common prefixes into variables
				//we do this instead of using the original string, as the result is simpler
				//and easier to understand
				var replacement = GetLongestReplacement (s, prefixes);
				if (!replacement.HasValue) {
					sb.Append (GLib.Markup.EscapeText (s));
					continue;
				}
				sb.Append ($"<span foreground=\"{varColor}\">{GLib.Markup.EscapeText (replacement.Value.subst)}</span>");
				sb.Append (GLib.Markup.EscapeText (s.Substring (replacement.Value.prefix.Length)));
			}
			return sb?.ToString () ?? baseDesc;
		}

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
					if (!longestReplacement.HasValue|| longestReplacement.Value.prefix.Length < replacement.prefix.Length) {
						longestReplacement = replacement;
					}
				}
			}
			return longestReplacement;
		}
	}
}