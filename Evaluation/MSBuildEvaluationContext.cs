//
// MSBuildEvaluationContext.cs
//
// Author:
//       Lluis Sanchez Gual <lluis@xamarin.com>
//
// Copyright (c) 2014 Xamarin, Inc (http://www.xamarin.com)
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MonoDevelop.Core;
using MonoDevelop.Core.Assemblies;
using MonoDevelop.MSBuildEditor.ExpressionParser;
using MonoDevelop.Projects.MSBuild;
using BF = System.Reflection.BindingFlags;

namespace MonoDevelop.MSBuildEditor
{
	class MSBuildEvaluationContext
	{
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

		public static MSBuildEvaluationContext Create (MSBuildToolsVersion toolsVersion, TargetRuntime runtime, MSBuildSdkResolver sdkResolver, string projectPath, string thisFilePath)
		{
			// MSBuildEvaluationContext can only populate these properties from an MSBuildProject and we don't have one
			// OTOH this isn't a full evaluation anyway. Just set up a bunch of properties commonly used for imports.
			// TODO: add more commonly used properties
			var ctx = new MSBuildEvaluationContext ();

			string tvString = toolsVersion.ToVersionString ();
			string binPath = runtime.GetMSBuildBinPath (tvString);
			ctx.SetPropertyValue ("MSBuildBinPath", binPath);
			ctx.SetPropertyValue ("MSBuildToolsPath", binPath);
			ctx.SetPropertyValue ("MSBuildToolsVersion", tvString);
			var extPath = MSBuildProjectService.ToMSBuildPath (null, runtime.GetMSBuildExtensionsPath ());
			ctx.SetPropertyValue ("MSBuildExtensionsPath", extPath);
			ctx.SetPropertyValue ("MSBuildExtensionsPath32", extPath);
			ctx.SetPropertyValue ("MSBuildProjectDirectory", MSBuildProjectService.ToMSBuildPath (null, Path.GetDirectoryName (projectPath)));
			ctx.SetPropertyValue ("MSBuildThisFileDirectory", MSBuildProjectService.ToMSBuildPath (null, Path.GetDirectoryName (thisFilePath) + Path.DirectorySeparatorChar));

			var defaultSdksPath = sdkResolver.DefaultSdkPath;
			if (defaultSdksPath != null) {
				ctx.SetPropertyValue ("MSBuildSDKsPath", MSBuildProjectService.ToMSBuildPath (null, defaultSdksPath));
			}

			ctx.extensionPaths = new List<string> { extPath };
			if (Platform.IsMac) {
				ctx.extensionPaths.Add ("/Library/Frameworks/Mono.framework/External/xbuild");
			}

			return ctx;
		}

		List<string> extensionPaths;

		internal string EvaluatePath (string path, string basePath)
		{
			string filename = Evaluate (path);
			return MSBuildProjectService.FromMSBuildPath (basePath, filename);
		}

		internal IEnumerable<string> EvaluatePathWithPermutation(string path, string basePath, PropertyValueCollector propVals)
		{
			//TODO: support wildcards
			if (path.IndexOf ('*') != -1) {
				yield break;
			}

			//fast path for imports without properties, will generally be true for SDK imports
			if (path.IndexOf ('$') < 0) {
				yield return EvaluatePath (path, basePath);
				yield break;
			}

			if (propVals == null) {
				yield return EvaluatePath (path, basePath);
				yield break;
			}

			//ensure each of the properties is fully evaluated
			//FIXME this is super hacky, use real MSBuild evaluation
			foreach (var p in propVals) {
				if (p.Value != null) {
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
							p.Value [i] = val;
						} catch (Exception ex) {
							LoggingService.LogError ($"Error evaluating property {p.Key}={val}", ex);
						}
					}
				}
			}

			//TODO: use a new context instead of altering this one?
			foreach (var p in propVals) {
				if (p.Value != null) {
					SetPropertyValue (p.Key, p.Value [0]);
				}
			}

			//permute on properties for which we have multiple values
			var expr = new Expression ();
			expr.Parse (path, ParseOptions.None);
			var propsToPermute = new List<(string, List<string>)> ();
			foreach (var prop in expr.Collection.OfType<PropertyReference> ()) {
				if (propVals.TryGetValues (prop.Name, out List<string> values) && values != null) {
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
				if (!seen.Add (val)) {
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