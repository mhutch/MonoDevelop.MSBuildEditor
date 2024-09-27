// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Text;

namespace MonoDevelop.MSBuild.Language
{
	// TODO: move this to MonoDevelop.Xml
	class XmlEscaping
	{
		static readonly Dictionary<string, char> entities = new Dictionary<string, char> {
			{ "lt", '<' },
			{ "gt", '>' },
			{ "amp", '&' },
			{ "quot", '"' },
			{ "apos", '\'' }
		};

		public static string UnescapeEntities (string xmlString)
		{
			if (xmlString.IndexOf (';') < 0) {
				return xmlString;
			}

			var sb = new StringBuilder (xmlString.Length);
			for (int i = 0; i < xmlString.Length; i++) {
				char c = xmlString [i];
				if (c == '&') {
					var end = xmlString.IndexOf (';', i + 1);
					if (end > -1) {
						var s = xmlString.Substring (i + 1, end - i - 1);
						if (entities.TryGetValue (s, out char conv)) {
							sb.Append (conv);
							i = end;
							continue;
						}
					}
				}
				sb.Append (c);
			}
			return sb.ToString ();
		}
	}
}
