// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MonoDevelop.Core;
using MonoDevelop.MSBuildEditor.Language;
using MonoDevelop.Projects.MSBuild;
using BF = System.Reflection.BindingFlags;

namespace MonoDevelop.MSBuildEditor.Evaluation
{
	class MSBuildEvaluationContext
	{
		static HashSet<string> readonlyProps = new HashSet<string> (StringComparer.OrdinalIgnoreCase);

		object wrapped;
		MethodInfo evaluateMeth, setPropMeth, getPropMeth;

		public MSBuildEvaluationContext ()
		{
			var type = typeof (MSBuildItem).Assembly.GetType ("MonoDevelop.Projects.MSBuild.MSBuildEvaluationContext", true);
			evaluateMeth = type.GetMethod ("Evaluate", BF.Instance | BF.NonPublic | BF.Public, null, CallingConventions.Any, new [] { typeof (string) }, null);
			setPropMeth = type.GetMethod ("SetPropertyValue", BF.Instance | BF.NonPublic | BF.Public, null, CallingConventions.Any, new [] { typeof (string), typeof (string) }, null);
			getPropMeth = type.GetMethod ("GetPropertyValue", BF.Instance | BF.NonPublic | BF.Public, null, CallingConventions.Any, new [] { typeof (string) }, null);
			wrapped = Activator.CreateInstance (type);
		}

		internal string Evaluate (string value)
		{
			return (string)evaluateMeth.Invoke (wrapped, new [] { value });
		}

		internal void SetPropertyValue (string name, string value)
		{
			setPropMeth.Invoke (wrapped, new [] { name, value });
		}

		internal string GetPropertyValue (string name)
		{
			return (string)getPropMeth.Invoke (wrapped, new [] { name });
		}

		public static MSBuildEvaluationContext Create (IRuntimeInformation runtime, string projectPath, string thisFilePath)
		{
			// MSBuildEvaluationContext can only populate these properties from an MSBuildProject and we don't have one
			// OTOH this isn't a full evaluation anyway. Just set up a bunch of properties commonly used for imports.
			// TODO: add more commonly used properties
			var ctx = new MSBuildEvaluationContext ();

			string tvString = MSBuildToolsVersion.Unknown.ToVersionString ();
			string binPath = MSBuildProjectService.ToMSBuildPath (null, runtime.GetBinPath ());
			string toolsPath = MSBuildProjectService.ToMSBuildPath (null, runtime.GetToolsPath ());

			var extPaths = runtime.GetExtensionsPaths ();
			ctx.extensionPaths = new List<string> (extPaths);

			AddReadOnlyProp ("MSBuildBinPath", binPath);
			AddReadOnlyProp ("MSBuildToolsPath", toolsPath);
			AddReadOnlyProp ("MSBuildToolsPath32", toolsPath);
			AddReadOnlyProp ("MSBuildToolsPath64", toolsPath);
			AddReadOnlyProp ("RoslynTargetsPath", $"{binPath}\\Roslyn");
			AddReadOnlyProp ("MSBuildToolsVersion", tvString);
			var extPath = MSBuildProjectService.ToMSBuildPath (null, extPaths.First());
			AddReadOnlyProp ("MSBuildExtensionsPath", extPath);
			AddReadOnlyProp ("MSBuildExtensionsPath32", extPath);
			AddReadOnlyProp ("VisualStudioVersion", "15.0");

			var defaultSdksPath = runtime.GetSdksPath ();
			if (defaultSdksPath != null) {
				AddReadOnlyProp ("MSBuildSDKsPath", MSBuildProjectService.ToMSBuildPath (null, defaultSdksPath));
			}

			// project path properties
			string escapedProjectDir = MSBuildProjectService.ToMSBuildPath (null, Path.GetDirectoryName (projectPath));
			AddReadOnlyProp ("MSBuildProjectDirectory", escapedProjectDir);
			// "MSBuildProjectDirectoryNoRoot" is this actually used for anything?
			AddReadOnlyProp ("MSBuildProjectExtension", MSBuildProjectService.EscapeString (Path.GetExtension (projectPath)));
			AddReadOnlyProp ("MSBuildProjectFile", MSBuildProjectService.EscapeString (Path.GetFileName (projectPath)));
			AddReadOnlyProp ("MSBuildProjectFullPath", MSBuildProjectService.ToMSBuildPath (null, Path.GetFullPath (projectPath)));
			AddReadOnlyProp ("MSBuildProjectName", MSBuildProjectService.EscapeString (Path.GetFileNameWithoutExtension (projectPath)));

			//don't have a better value, this is as good as anything
			AddReadOnlyProp ("MSBuildStartupDirectory", escapedProjectDir);

			// this file path properties
			AddReadOnlyProp ("MSBuildThisFile", MSBuildProjectService.EscapeString (Path.GetFileName (thisFilePath)));
			AddReadOnlyProp ("MSBuildThisFileDirectory", MSBuildProjectService.ToMSBuildPath (null, Path.GetDirectoryName (thisFilePath)) + "\\");
			//"MSBuildThisFileDirectoryNoRoot" is this actually used for anything?
			AddReadOnlyProp ("MSBuildThisFileExtension", MSBuildProjectService.EscapeString (Path.GetExtension (thisFilePath)));
			AddReadOnlyProp ("MSBuildThisFileFullPath", MSBuildProjectService.ToMSBuildPath (null, Path.GetFullPath (thisFilePath)));
			AddReadOnlyProp ("MSBuildThisFileName", MSBuildProjectService.EscapeString (Path.GetFileNameWithoutExtension (thisFilePath)));

			//HACK: we don't get a usable value for this without real evaluation so hardcode 'obj'
			var projectExtensionsPath = Path.GetFullPath (Path.Combine (Path.GetDirectoryName (projectPath), "obj"));
			AddReadOnlyProp ("MSBuildProjectExtensionsPath", MSBuildProjectService.ToMSBuildPath (null, projectExtensionsPath) + "\\");

			return ctx;

			void AddReadOnlyProp (string key, string val)
			{
				ctx.SetPropertyValue (key, val);
				readonlyProps.Add (key);
			}
		}

		List<string> extensionPaths;

		internal string EvaluatePath (string path, string basePath)
		{
			string filename = Evaluate (path);
			return MSBuildProjectService.FromMSBuildPath (basePath, filename);
		}

		// FIXME: need to make this more efficient. can we ignore results where a property was simply not found?
		// can we tokenize it and check each level of the path exists before drilling down?
		internal IEnumerable<string> EvaluatePathWithPermutation (string path, string basePath, PropertyValueCollector propVals)
		{
			//fast path for imports without properties, will generally be true for SDK imports
			if (path.IndexOf ('$') < 0) {
				yield return EvaluatePath (path, basePath);
				yield break;
			}

			if (propVals != null) {
				//ensure each of the properties is fully evaluated
				//FIXME this is super hacky, use real MSBuild evaluation
				foreach (var p in propVals) {
					if (p.Value != null && !readonlyProps.Contains (p.Key)) {
						for (int i = 0; i < p.Value.Count; i++) {
							var val = p.Value [i];
							int recDepth = 0;
							try {
								while (val.IndexOf ('$') > -1 && (recDepth++ < 10)) {
									val = Evaluate (val);
								}
								if (val != null && val.IndexOf ('$') < 0) {
									SetPropertyValue (p.Key, val);
								}
								if (string.IsNullOrEmpty (val)) {
									p.Value.RemoveAt (i);
									i--;
								} else {
									p.Value [i] = val;
								}
							} catch {
								//this happens a lot with things like property functions that
								//index into null values, so make it quiet
								//LoggingService.LogDebug ($"Error evaluating property {p.Key}={val}");
								//FIXME stop ignoring these errors
							}
						}
					}
				}

				//TODO: use a new context instead of altering this one?
				foreach (var p in propVals) {
					if (p.Value != null && p.Value.Count > 0 && !readonlyProps.Contains(p.Key)) {
						SetPropertyValue (p.Key, p.Value [0]);
					}
				}
			}

			//permute on properties for which we have multiple values
			var expr = ExpressionParser.Parse (path, ExpressionOptions.None);
			var propsToPermute = new List<(string, List<string>)> ();
			foreach (var prop in expr.WithAllDescendants ().OfType<ExpressionProperty> ()) {
				if (readonlyProps.Contains (prop.Name)) {
					continue;
				}
				if (propVals != null && propVals.TryGetValues (prop.Name, out List<string> values) && values != null) {
					if (values.Count > 1) {
						propsToPermute.Add ((prop.Name, values));
					}
				} else if (extensionPaths != null && string.Equals (prop.Name, "MSBuildExtensionsPath", StringComparison.OrdinalIgnoreCase) || string.Equals (prop.Name, "MSBuildExtensionsPath32", StringComparison.OrdinalIgnoreCase)) {
					propsToPermute.Add ((prop.Name, extensionPaths));
				}
			}

			if (propsToPermute.Count == 0) {
				yield return EvaluatePath (path, basePath);
			} else {
				foreach (var ctx in PermuteProperties (this, propsToPermute)) {
					yield return EvaluatePath (path, basePath);
				}
			}
		}

		//TODO: guard against excessive permutation
		//TODO: return a new context instead of altering this one?
		static IEnumerable<MSBuildEvaluationContext> PermuteProperties (MSBuildEvaluationContext evalCtx, List<(string, List<string>)> multivaluedProperties, int idx = 0)
		{
			var prop = multivaluedProperties [idx];
			var name = prop.Item1;
			// the list may contain multiple of the same item
			// we don't just convert it into a hashset as it needs to preserve order
			var seen = new HashSet<string> ();
			foreach (var val in prop.Item2) {
				if (!seen.Add (val) || string.IsNullOrEmpty (val)) {
					continue;
				}
				evalCtx.SetPropertyValue (name, val);
				if (idx + 1 == multivaluedProperties.Count) {
					yield return evalCtx;
				} else {
					foreach (var permutation in PermuteProperties (evalCtx, multivaluedProperties, idx + 1)) {
						yield return permutation;
					}
				}
			}
		}
	}
}