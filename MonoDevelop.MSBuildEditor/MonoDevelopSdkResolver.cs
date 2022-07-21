// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Build.Framework;
using MonoDevelop.Core;
using MonoDevelop.Core.Assemblies;
using MonoDevelop.MSBuild.SdkResolution;
using MonoDevelop.Projects.MSBuild;
using BF = System.Reflection.BindingFlags;

namespace MonoDevelop.MSBuildEditor
{
	/// <summary>
	/// Exposes internal resolver APIs from MD core.
	/// </summary>
	class MonoDevelopSdkResolver
	{
		TargetRuntime runtime;
		List<SdkInfo> registeredSdks;
		object logger, resolver;
		MethodInfo resolveMeth;

		// FIXME: this is supposed to be specific to each (projectFile, solutionFile), and performs caching on that basis
		object evalCtx;

		public string DefaultSdkPath { get; }

		public MonoDevelopSdkResolver (TargetRuntime runtime)
		{
			this.runtime = runtime;

			var asm = typeof (MSBuildProjectService).Assembly;
			Type GetTypeOrFail (string typeName) => asm.GetType (typeName) ?? throw new Exception ($"Failed to get type ${typeName}");

			var defaultSdksMeth = typeof (MSBuildProjectService).GetMethod ("GetDefaultSdksPath", BF.NonPublic | BF.Static);
			DefaultSdkPath = (string)defaultSdksMeth?.Invoke (null, new object [] { runtime });
			if (DefaultSdkPath is null) {
				throw new Exception ("Failed to get MSBuild default SDK from MSBuildProjectService");
			}

			var logServiceInterface = GetTypeOrFail ("MonoDevelop.Projects.MSBuild.ILoggingService");
			var logServiceType = GetTypeOrFail ("MonoDevelop.Projects.MSBuild.CustomLoggingService");
			var logInstanceField = logServiceType?.GetField ("Instance", BF.Public | BF.Static);
			logger = logInstanceField?.GetValue (null);
			if (logger is null || !logServiceType.IsAssignableTo (logServiceInterface)) {
				throw new Exception ("Failed to get MonoDevelop.Projects.MSBuild.CustomLoggingService instance");
			}

			var resolverType = asm.GetType ("MonoDevelop.Projects.MSBuild.SdkResolution");
			var getResolverMeth = resolverType?.GetMethod ("GetResolver", BF.Public | BF.Static);
			resolver = getResolverMeth?.Invoke (null, new [] { runtime });
			if (resolver is null) {
				throw new Exception ("Failed to get MonoDevelop.Projects.MSBuild.SdkResolution instance");
			}

			var evalCtxType = asm.GetType ("MonoDevelop.Projects.MSBuild.MSBuildEvaluationContext");
			var evalCtxCtor = evalCtxType.GetConstructor (new Type[] { });
			evalCtx = evalCtxCtor.Invoke (null);
			if (evalCtx is null) {
				throw new Exception ("Failed to create MonoDevelop.Projects.MSBuild.MSBuildEvaluationContext instance");
			}

			resolveMeth = resolverType?.GetMethod ("Resolve", BF.NonPublic | BF.Public | BF.Instance, new Type[] {
				typeof(SdkReference),
				logServiceInterface,
				evalCtxType,
				GetTypeOrFail("MonoDevelop.Projects.MSBuild.MSBuildContext"),
				typeof(string),
				typeof(string)
			});
			if (resolveMeth == null || !resolveMeth.ReturnType.IsAssignableTo(typeof(SdkResult))) {
				throw new Exception ("Failed to get MonoDevelop.Projects.MSBuild.SdkResolution.Resolve method");
			}
		}

		public SdkResult Resolve (SdkReference sdk, string projectFile, string solutionPath)
		{
			// Resolve (SdkReference sdk, ILoggingService logger, MSBuildEvaluationContext evaluationContext, MSBuildContext? buildEventContext, string projectFile, string solutionFile)
			return (SdkResult) resolveMeth.Invoke (resolver, new[] { sdk, logger, evalCtx, null, projectFile, solutionPath });
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
					var version = (MonoDevelop.Projects.MSBuild.SdkVersion)sdkInfoVersionProp.GetValue (sdkInfo);
					var path = (string)sdkInfoPathProp.GetValue (sdkInfo);

					registeredSdks.Add (
						new SdkInfo (
							name,
							version?.ToString (),
							path
						)
					);
				}
			} catch (Exception ex) {
				LoggingService.LogError ("Failed to find registered MSBuild SDKs", ex);
			}

			return registeredSdks;
		}
	}
}
