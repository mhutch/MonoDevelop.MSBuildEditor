//
// Copyright (c) 2017 Microsoft Corp.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MonoDevelop.Core.Assemblies;
using MonoDevelop.Ide.Editor;
using MonoDevelop.Xml.Dom;
using NuGet.Frameworks;

namespace MonoDevelop.MSBuildEditor
{
	class PropertyValueCollector : IEnumerable<KeyValuePair<string, List<string>>>
	{
		Dictionary<string, List<string>> props = new Dictionary<string, List<string>> ();

		public PropertyValueCollector (bool collectTargetFrameworks)
		{
			if (collectTargetFrameworks) {
				Mark ("TargetFramework");
				Mark ("TargetFrameworks");
				Mark ("_ShortFrameworkVersion");
				Mark ("_ShortFrameworkIdentifier");
				Mark ("TargetFrameworkIdentifier");
				Mark ("TargetFrameworkVersion");
				Mark ("TargetFrameworkProfile");
				Mark ("TargetFrameworkMoniker");
			}
		}

		public IEnumerable<List<string>> Values => props.Values;

		public void Collect (string name, XElement el, ITextDocument textDocument)
		{
			if (props.TryGetValue (name, out List<string> values) && el.IsClosed && !el.IsSelfClosing && el.Region.End < el.ClosingTag.Region.Begin) {
				if (values == null) {
					props [name] = values = new List<string> ();
				}
				var val = textDocument.GetTextBetween (el.Region.End, el.ClosingTag.Region.Begin);
				values.Add (val);
			}
		}

		public void Mark (string name)
		{
			if (!props.ContainsKey (name)) {
				props [name] = null;
			}
		}

		public IEnumerator<KeyValuePair<string, List<string>>> GetEnumerator ()
		{
			return props.GetEnumerator ();
		}

		IEnumerator IEnumerable.GetEnumerator ()
		{
			return props.GetEnumerator ();
		}

		internal bool TryGetValues (string name, out List<string> values)
		{
			return props.TryGetValue (name, out values) && values != null;
		}

		public List<TargetFrameworkMoniker> GetFrameworks ()
		{
			var list = new List<TargetFrameworkMoniker> ();
			if (TryGetValues ("TargetFrameworks", out List<string> multiFxList)) {
				foreach (var multiFxStr in multiFxList) {
					if (multiFxStr != null && IsConstExpr (multiFxStr)) {
						var multiFxArr = multiFxStr.Split (new [] { ';' }, StringSplitOptions.RemoveEmptyEntries);
						foreach (var fxstr in multiFxArr) {
							var fx = NuGetFramework.ParseFolder (fxstr);
							if (fx.IsSpecificFramework) {
								list.Add (TargetFrameworkMoniker.Parse (fx.DotNetFrameworkName));
							}
						}
					}
				}
				if (list.Count > 0) {
					return list;
				}
			}
			if (TryGetValues ("TargetFramework", out List<string> fxList)) {
				foreach (var fxstr in fxList) {
					if (IsConstExpr (fxstr)) {
						var fx = NuGetFramework.ParseFolder (fxstr);
						if (fx.IsSpecificFramework) {
							list.Add (TargetFrameworkMoniker.Parse (fx.DotNetFrameworkName));
							return list;
						}
					}
				}
			}

			if (TryGetValues ("TargetFrameworkIdentifier", out List<string> idList) && TryGetValues ("TargetFrameworkVersion", out List<string> versionList)) {
				var id = idList.FirstOrDefault (IsConstExpr);
				var version = versionList.FirstOrDefault (v => {
					if (v [0] == 'v') {
						v = v.Substring (1);
					}
					return IsConstExpr (v) && Version.TryParse (v, out _);
				});

				if (!string.IsNullOrEmpty (version) && !string.IsNullOrEmpty (id)) {
					if (TryGetValues ("TargetFrameworkProfile", out List<string> profileList)) {
						var profile = profileList.FirstOrDefault (IsConstExpr);
						list.Add (new TargetFrameworkMoniker (id, version, profileList[0]));
						return list;
					}
					list.Add (new TargetFrameworkMoniker (id, version));
					return list;
				}
			}

			return list;

			bool IsConstExpr (string p) => p.IndexOf ('$') < 0;
		}

	}
}