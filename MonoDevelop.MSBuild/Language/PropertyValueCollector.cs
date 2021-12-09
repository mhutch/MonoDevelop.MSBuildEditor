// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MonoDevelop.MSBuild.Language.Expressions;
using NuGet.Frameworks;

namespace MonoDevelop.MSBuild.Language
{
	class PropertyValueCollector : IEnumerable<KeyValuePair<string, List<ExpressionNode>>>
	{
		Dictionary<string, List<ExpressionNode>> props = new Dictionary<string, List<ExpressionNode>> ();

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

				//HACK hardcode these, the values are used in imports of the file that defines them
				//so the automatic simple marking doesn't work
				//TODO move this to a the buildschema?
				//TODO set MicrosoftNETBuildTasksTFM to net46?
				Mark ("MicrosoftNETBuildTasksDirectoryRoot");
				Mark ("MicrosoftNETBuildTasksTFM");
				Mark ("MicrosoftNETBuildTasksDirectory");
				Mark ("MicrosoftNETBuildExtensionsTasksAssembly");
				Mark ("MicrosoftNETBuildTasksAssembly");

				// this is needed for PackageReference-provided imports to load from the global cache
				Mark ("NuGetPackageRoot");
			}
		}

		public IEnumerable<List<ExpressionNode>> Values => props.Values;

		public void Collect (string name, ExpressionNode value)
		{
			if (value == null) {
				return;
			}
			if (props.TryGetValue (name, out List<ExpressionNode> values) || true) {
				if (values == null) {
					props [name] = values = new List<ExpressionNode> ();
				}
				values.Add (value);
			}
		}

		public void Mark (string name)
		{
			if (!props.ContainsKey (name)) {
				props [name] = null;
			}
		}

		public IEnumerator<KeyValuePair<string, List<ExpressionNode>>> GetEnumerator ()
		{
			return props.GetEnumerator ();
		}

		IEnumerator IEnumerable.GetEnumerator ()
		{
			return props.GetEnumerator ();
		}

		internal bool TryGetValues (string name, out List<ExpressionNode> values)
		{
			return props.TryGetValue (name, out values) && values != null && values.Count > 0;
		}

		public List<NuGetFramework> GetFrameworks ()
		{
			var list = new List<NuGetFramework> ();

			void CaptureFramework (ExpressionNode fxExpr)
			{
				if (fxExpr is ExpressionText fxStr) {
					var fx = NuGetFramework.ParseFolder (fxStr.Value);
					if (fx != null && fx.IsSpecificFramework) {
						list.Add (fx);
					}
				}
			}

			if (TryGetValues ("TargetFrameworks", out List<ExpressionNode> multiFxList)) {
				foreach (var multiFxExpr in multiFxList) {
					if (multiFxExpr is ListExpression multiFxArr) {
						foreach (var fxExpr in multiFxArr.Nodes) {
							CaptureFramework (fxExpr);
						}
					} else if (multiFxExpr is ExpressionText fxExpr) {
						CaptureFramework (fxExpr);
					}
				}
				if (list.Count > 0) {
					return list;
				}
			}
			if (TryGetValues ("TargetFramework", out List<ExpressionNode> fxList)) {
				foreach (var fxExpr in fxList) {
					CaptureFramework (fxExpr);
				}
			}

			if (TryGetValues ("TargetFrameworkIdentifier", out List<ExpressionNode> idList) && TryGetValues ("TargetFrameworkVersion", out List<ExpressionNode> versionList)) {
				var id = (idList.FirstOrDefault (IsConstExpr) as ExpressionText)?.Value;
				var version = versionList.OfType<ExpressionText> ().Select (v => {
					string s = v.Value;
					if (s[0] == 'v') {
						s = s.Substring (1);
					}
					if (IsConstExpr (v) && Version.TryParse (s, out Version parsed)) {
						return parsed;
					}
					return null;
				}).FirstOrDefault (v => v != null);

				if (version != null && !string.IsNullOrEmpty (id)) {
					if (TryGetValues ("TargetFrameworkProfile", out List<ExpressionNode> profileList)) {
						var profile = profileList.FirstOrDefault (IsConstExpr) as ExpressionText;
						if (profile != null) {
							list.Add (new NuGetFramework (id, version, profile.Value));
							return list;
						}
					}
					list.Add (new NuGetFramework (id, version, null));
					return list;
				}
			}

			return list;

			bool IsConstExpr (ExpressionNode n) => n is ExpressionText;
		}

	}
}