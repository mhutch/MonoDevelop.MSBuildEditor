// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MonoDevelop.MSBuild.Evaluation;
using MonoDevelop.MSBuild.Language.Expressions;
using NuGet.Frameworks;

namespace MonoDevelop.MSBuild.Language
{
	class PropertyValueCollector : IEnumerable<KeyValuePair<string, List<EvaluatedValue>>>
	{
		readonly Dictionary<string, List<EvaluatedValue>> props = new (StringComparer.OrdinalIgnoreCase);

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

				Mark ("_DirectoryBuildPropsBasePath");
				Mark ("_DirectoryBuildPropsFile");
			}
		}

		public IEnumerable<List<EvaluatedValue>> Values => props.Values;


		// TODO: make this smarter. should avoid collecting same value twice, and outright replace values when the new value is not conditioned in any way.
		public void Collect (IMSBuildEvaluationContext fileEvaluationContext, string name, ExpressionNode value)
		{
			if (value == null) {
				return;
			}

			const bool collectAll = false;

			if (props.TryGetValue (name, out List<EvaluatedValue> values) || collectAll) {

				var combinedContext = new MSBuildCollectedValuesEvaluationContext (fileEvaluationContext, this);

				if (values == null) {
					props [name] = values = new List<EvaluatedValue> ();
				}

				foreach (var val in combinedContext.EvaluateWithPermutation (value).ToList ()) {
					if (!string.IsNullOrEmpty (val)) {
						values.Add (new EvaluatedValue (val));
					}
				}
			}
		}

		public void Mark (string? name)
		{
			if (name == null)
				return;

			if (!props.ContainsKey (name)) {
				props [name] = null;
			}
		}

		public IEnumerator<KeyValuePair<string, List<EvaluatedValue>>> GetEnumerator () => props.GetEnumerator ();

		IEnumerator IEnumerable.GetEnumerator () => props.GetEnumerator ();

		internal bool TryGetValues (string name, out List<EvaluatedValue> values)
		{
			return props.TryGetValue (name, out values) && values != null && values.Count > 0;
		}

		public List<NuGetFramework> GetFrameworks ()
		{
			var list = new List<NuGetFramework> ();

			void CaptureFramework (string fxStr)
			{
				var fx = NuGetFramework.ParseFolder (fxStr);
				if (fx != null && fx.IsSpecificFramework) {
					list.Add (fx);
				}
			}

			if (TryGetValues ("TargetFrameworks", out List<EvaluatedValue> multiFxList)) {
				foreach (var multiFxExpr in multiFxList) {
					foreach (var fxExpr in multiFxExpr.EscapedValue.Split (';')) {
						CaptureFramework (fxExpr);
					}
				}
				if (list.Count > 0) {
					return list;
				}
			}

			if (TryGetValues ("TargetFramework", out List<EvaluatedValue> fxList)) {
				foreach (var fxExpr in fxList) {
					CaptureFramework (fxExpr.EscapedValue);
				}
			}

			if (TryGetValues ("TargetFrameworkIdentifier", out List<EvaluatedValue> idList) && TryGetValues ("TargetFrameworkVersion", out List<EvaluatedValue> versionList)) {
				var id = idList[0].EscapedValue;
				var version = versionList.Select (v => {
					string s = v.Unescape();
					if (s is null) {
						return null;
					}
					if (s[0] == 'v') {
						s = s.Substring (1);
					}
					if (Version.TryParse (s, out Version parsed)) {
						return parsed;
					}
					return null;
				}).FirstOrDefault (v => v != null);

				if (version != null && !string.IsNullOrEmpty (id)) {
					if (TryGetValues ("TargetFrameworkProfile", out List<EvaluatedValue> profileList)) {
						var profile = profileList[0].EscapedValue;
						list.Add (new NuGetFramework (id, version, profile));
						return list;
					}
					list.Add (new NuGetFramework (id, version, null));
					return list;
				}
			}

			return list;
		}
	}
}