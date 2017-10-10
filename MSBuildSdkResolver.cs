//
// Copyright (c) Microsoft Corp.
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
using MonoDevelop.Core;
using BF = System.Reflection.BindingFlags;
using MonoDevelop.Projects.MSBuild;
using System.Collections.Generic;
using MonoDevelop.Core.Assemblies;
using System.Collections;
using Microsoft.Build.Framework;
using System.Reflection;

namespace MonoDevelop.MSBuildEditor
{
	/// <summary>
	/// Exposes internal resolver APIs from MD core.
	/// </summary>
	class MSBuildSdkResolver
	{
		TargetRuntime runtime;
		List<SdkInfo> registeredSdks;
		object logger, resolver;
		MethodInfo getSdkMeth;

		public string DefaultSdkPath { get; }

		public MSBuildSdkResolver (TargetRuntime runtime)
		{
			this.runtime = runtime;

			try {
				var defaultSdksMeth = typeof (MSBuildProjectService).GetMethod ("GetDefaultSdksPath", BF.NonPublic | BF.Static);
				DefaultSdkPath = (string)defaultSdksMeth.Invoke (null, new object [] { runtime });
			} catch (Exception ex) {
				LoggingService.LogError ("Failed to get MSBuild default SDK", ex);
			}

			try {
				var asm = typeof (MSBuildProjectService).Assembly;
				var logService = asm.GetType ("MonoDevelop.Projects.MSBuild.CustomLoggingService");
				var logInstanceField = logService.GetField ("Instance", BF.Public | BF.Static);
				logger = logInstanceField.GetValue (null);

				var resolverType = asm.GetType ("MonoDevelop.Projects.MSBuild.SdkResolution");
				var getResolverMeth = resolverType.GetMethod ("GetResolver", BF.Public | BF.Static);
				resolver = getResolverMeth.Invoke (null, new [] { runtime });

				getSdkMeth = resolverType.GetMethod ("GetSdkPath", BF.NonPublic | BF.Public | BF.Instance);

			} catch (Exception ex) {
				LoggingService.LogError ("Failed to get MSBuild SdkResolution", ex);
			}
		}

		/// <summary>
		/// Gets SDKs registered by MonoDevelop extensions.
		/// </summary>
		/// <returns>The registered sdks.</returns>
		public List<SdkInfo> GetRegisteredSdks ()
		{
			if (registeredSdks != null) {
				return registeredSdks;
			}
			registeredSdks = new List<SdkInfo> ();

			try {
				var registeredSdksMeth = typeof (MSBuildProjectService).GetMethod ("FindRegisteredSdks", BF.NonPublic | BF.Static);
				var sdkInfoClass = registeredSdksMeth.ReturnType.GenericTypeArguments [0];
				var sdkInfoNameProp = sdkInfoClass.GetProperty ("Name");
				var sdkInfoVersionProp = sdkInfoClass.GetProperty ("Version");
				var sdkInfoPathProp = sdkInfoClass.GetProperty ("Path");

				var registeredSdksEnumerable = (IEnumerable)registeredSdksMeth.Invoke (null, null);
				foreach (var sdkInfo in registeredSdksEnumerable) {
					var name = (string)sdkInfoNameProp.GetValue (sdkInfo);
					var version = (SdkVersion)sdkInfoVersionProp.GetValue (sdkInfo);
					var path = (string)sdkInfoPathProp.GetValue (sdkInfo);
					registeredSdks.Add (new SdkInfo (name, version, path));
				}
			} catch (Exception ex) {
				LoggingService.LogError ("Failed to find registered MSBuild SDKs", ex);
			}

			return registeredSdks;
		}

		public string GetSdkPath (SdkReference sdk, string projectFile, string solutionPath)
		{
			// GetSdkPath (SdkReference sdk, ILoggingService logger, MSBuildContext buildEventContext, string projectFile, string solutionPath)
			return (string) getSdkMeth.Invoke (resolver, new [] { sdk, logger, null, projectFile, solutionPath });
		}

		public class SdkInfo
		{
			public SdkInfo (string name, SdkVersion version, string path)
			{
				Name = name;
				Version = version;
				Path = path;
			}

			public string Name { get; }
			public SdkVersion Version { get; }
			public string Path { get; }
		}
	}
}
