// Copyright (c) 2014 Xamarin Inc.
// Copyright (c) Microsoft. ALl rights reserved.
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
		readonly MSBuildResolveContext ctx;
		readonly BaseInfo info;
		string description;

		public MSBuildCompletionData (BaseInfo info, MSBuildResolveContext ctx, DataType type)
			: base (info.Name, info.Description, type)
		{
			this.info = info;
			this.ctx = ctx;
		}

		public override string Description {
			get {
				return description ?? (description = GetDescription () ?? "");
			}
		}

		string GetDescription ()
		{
			return AppendSeenIn (base.Description);
		}

		string AppendSeenIn (string baseDesc)
		{
			if (ctx == null) {
				return baseDesc;
			}

			IEnumerable<string> seenIn = ctx.GetFilesSeenIn (info);
			StringBuilder sb = null;

			foreach (var s in seenIn) {
				if (sb == null) {
					sb = new StringBuilder ();
					if (!string.IsNullOrEmpty (baseDesc)) {
						sb.AppendLine (baseDesc);
						sb.AppendLine ();
					}
					sb.AppendLine ("Seen in: ");
					sb.AppendLine ();
				}
				sb.AppendLine ($"    {s}");
			}
			return sb?.ToString () ?? baseDesc;
		}
	}
}