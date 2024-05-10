// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
//  WARNING: THIS FILE WAS IMPORTED FROM MSBUILD
//
//  * IF YOU UPDATE IT, UPDATE THE HASH IN THE URL AND REAPPLY CHANGES FROM THE NOTES IN THIS HEADER
//  * IF YOU MODIFY IT, DESCRIBE THE CHANGES IN THE NOTES
//
//  URL: https://raw.githubusercontent.com/dotnet/msbuild/7434b575d12157ef98aeaad3b86c8f235f551c41/src/Build/BackEnd/Components/SdkResolution/SdkResolverLoader.cs
//
//  NOTE: the code in this file is the part of the MSBuildSdkResolver class that is copied from the
//        internal MSBuild class Microsoft.Build.BackEnd.SdkResolution.SdkResolverLoader, isolated to
//        a partial class to make it easier to update
//
//  CHANGES:
//    * change namespace to MonoDevelop.MSBuild.SdkResolution
//    * make class partial and change name to MSBuildSdkResolver
//    * use the ToolsPath32 property rather than BuildEnvironmentHelper.Instance.MSBuildToolsDirectory32
//    * pass the SDKsPath property to the DefaultSdkResolver ctor
//    * call location.LogWarning instead of throwing an exception via ProjectFileErrorUtilities.ThrowInvalidProjectFile
//      and return false where a return is necessary
//    * alias LoggingContext and ElementLocation to ILoggingService to noninvasively remove dependencies
//    * import the Microsoft.Build.BackEnd.SdkResolution namespace so other imported MSBuild internal types can be used

using Microsoft.Build.BackEnd.SdkResolution;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

using ILogger = Microsoft.Extensions.Logging.ILogger;
using Microsoft.Extensions.Logging;

namespace MonoDevelop.MSBuild.SdkResolution
{
    partial class MSBuildSdkResolver
    {
#if FEATURE_ASSEMBLYLOADCONTEXT
        private static readonly CoreClrAssemblyLoader s_loader = new CoreClrAssemblyLoader();
#endif

        private readonly string IncludeDefaultResolver = Environment.GetEnvironmentVariable("MSBUILDINCLUDEDEFAULTSDKRESOLVER");

        //  Test hook for loading SDK Resolvers from additional folders.  Support runtime-specific test hook environment variables,
        //  as an SDK resolver built for .NET Framework probably won't work on .NET Core, and vice versa.
        private readonly string AdditionalResolversFolder = Environment.GetEnvironmentVariable(
#if NETFRAMEWORK
            "MSBUILDADDITIONALSDKRESOLVERSFOLDER_NETFRAMEWORK"
#elif NET
            "MSBUILDADDITIONALSDKRESOLVERSFOLDER_NET"
#endif
            ) ?? Environment.GetEnvironmentVariable("MSBUILDADDITIONALSDKRESOLVERSFOLDER");

        internal virtual IList<SdkResolver> LoadResolvers(ILogger logger)
        {
            var resolvers = !String.Equals(IncludeDefaultResolver, "false", StringComparison.OrdinalIgnoreCase) ?
                new List<SdkResolver> {new DefaultSdkResolver(SDKsPath) }
                : new List<SdkResolver>();

            var potentialResolvers = FindPotentialSdkResolvers(Path.Combine(ToolsPath32, "SdkResolvers"), logger);

            if (potentialResolvers.Count == 0)
            {
                return resolvers;
            }

            foreach (var potentialResolver in potentialResolvers)
            {
                LoadResolvers(potentialResolver, logger, resolvers);
            }

            return resolvers.OrderBy(t => t.Priority).ToList();
        }

        /// <summary>
        ///     Find all files that are to be considered SDK Resolvers. Pattern will match
        ///     Root\SdkResolver\(ResolverName)\(ResolverName).dll.
        /// </summary>
        /// <param name="rootFolder"></param>
        /// <returns></returns>
        internal virtual IList<string> FindPotentialSdkResolvers(string rootFolder, ILogger logger)
        {
            var assembliesList = new List<string>();

            if ((string.IsNullOrEmpty(rootFolder) || !FileUtilities.DirectoryExistsNoThrow(rootFolder)) && AdditionalResolversFolder == null)
            {
                return assembliesList;
            }

            DirectoryInfo[] subfolders = GetSubfolders(rootFolder, AdditionalResolversFolder);

            foreach (var subfolder in subfolders)
            {
                var assembly = Path.Combine(subfolder.FullName, $"{subfolder.Name}.dll");
                var manifest = Path.Combine(subfolder.FullName, $"{subfolder.Name}.xml");

                var assemblyAdded = TryAddAssembly(assembly, assembliesList);
                if (!assemblyAdded)
                {
                    assemblyAdded = TryAddAssemblyFromManifest(manifest, subfolder.FullName, assembliesList, logger);
                }

                if (!assemblyAdded)
                {
					LogEmptySdkResolverFolder (logger, subfolder.FullName);
				}
            }

            return assembliesList;
        }

        private DirectoryInfo[] GetSubfolders(string rootFolder, string additionalResolversFolder)
        {
            DirectoryInfo[] subfolders = null;
            if (!string.IsNullOrEmpty(rootFolder) && FileUtilities.DirectoryExistsNoThrow(rootFolder))
            {
                subfolders = new DirectoryInfo(rootFolder).GetDirectories();
            }

            if (additionalResolversFolder != null)
            {
                var resolversDirInfo = new DirectoryInfo(additionalResolversFolder);
                if (resolversDirInfo.Exists)
                {
                    HashSet<DirectoryInfo> overrideFolders = resolversDirInfo.GetDirectories().ToHashSet(new DirInfoNameComparer());
                    if (subfolders != null)
                    {
                        overrideFolders.UnionWith(subfolders);
                    }
                    return overrideFolders.ToArray();
                }
            }

            return subfolders;
        }

        private class DirInfoNameComparer : IEqualityComparer<DirectoryInfo>
        {
            public bool Equals(DirectoryInfo first, DirectoryInfo second)
            {
                return string.Equals(first.Name, second.Name, StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode(DirectoryInfo value)
            {
                return value.Name.GetHashCode();
            }
        }

        private bool TryAddAssemblyFromManifest(string pathToManifest, string manifestFolder, List<string> assembliesList, ILogger logger)
        {
            if (!string.IsNullOrEmpty(pathToManifest) && !FileUtilities.FileExistsNoThrow(pathToManifest)) return false;

            string path = null;

            try
            {
                // <SdkResolver>
                //   <Path>...</Path>
                // </SdkResolver>
                var manifest = SdkResolverManifest.Load(pathToManifest);

                if (manifest == null || string.IsNullOrEmpty(manifest.Path))
                {
					LogCouldNotLoadSdkResolverUnspecifiedAssembly (logger, path);
					return false;
				}

                path = FileUtilities.FixFilePath(manifest.Path);
            }
            catch (XmlException ex)
            {
				// Note: Not logging e.ToString() as most of the information is not useful, the Message will contain what is wrong with the XML file.
				LogCouldNotLoadSdkResolverManifestInvalid (logger, pathToManifest, ex.Message);
				return false;
			}

            if (!Path.IsPathRooted(path))
            {
                path = Path.Combine(manifestFolder, path);
                path = Path.GetFullPath(path);
			}

			if (!TryAddAssembly(path, assembliesList)) {
				LogCouldNotLoadSdkResolverMissingAssembly(logger, pathToManifest, path);
			}

            return true;
        }

		bool TryAddAssembly(string assemblyPath, List<string> assembliesList)
		{
			if (string.IsNullOrEmpty (assemblyPath) || !FileUtilities.FileExistsNoThrow (assemblyPath)) {
				return false;
			}
			assembliesList.Add (assemblyPath);
			return true;
		}

        protected virtual IEnumerable<Type> GetResolverTypes(Assembly assembly)
        {
            return assembly.ExportedTypes
                .Select(type => new {type, info = type.GetTypeInfo()})
                .Where(t => t.info.IsClass && t.info.IsPublic && !t.info.IsAbstract && typeof(SdkResolver).IsAssignableFrom(t.type))
                .Select(t => t.type);
        }

        protected virtual Assembly LoadResolverAssembly(string resolverPath) => 
#if !FEATURE_ASSEMBLYLOADCONTEXT
            Assembly.LoadFrom(resolverPath);
#else
            s_loader.LoadFromPath(resolverPath);
#endif

        protected virtual void LoadResolvers(string resolverPath, ILogger logger, List<SdkResolver> resolvers)
        {
            Assembly assembly;
            try
            {
                assembly = LoadResolverAssembly(resolverPath);
            }
            catch (Exception ex)
            {
				LogSdkResolverTypeLoadFailed (logger, ex, resolverPath);
                return;
            }

            foreach (Type type in GetResolverTypes(assembly))
            {
                try
                {
                    resolvers.Add((SdkResolver)Activator.CreateInstance(type));
                }
                catch (Exception ex)
                {
                    // .NET wraps the original exception inside of a TargetInvocationException which masks the original message so get the inner exception in this case
					if (ex is TargetInvocationException invEx && invEx.InnerException is Exception innerEx) {
						ex = innerEx;
					}
					LogSdkResolverTypeLoadFailed(logger, ex, type.Name);
				}
			}
		}

		[LoggerMessage (EventId = 0, Level = LogLevel.Warning, Message = "SDK resolver type '{typeName}' failed to load")]
		static partial void LogSdkResolverTypeLoadFailed (ILogger logger, Exception ex, string typeName);

		[LoggerMessage (EventId = 1, Level = LogLevel.Warning, Message = "SDK resolver assembly '{assembly}' failed to load")]
		static partial void LogSdkResolverAssemblyLoadFailed (ILogger logger, Exception ex, string assembly);

		[LoggerMessage (EventId = 2, Level = LogLevel.Warning, Message = "Could not load SDK resolver as the manifest '{manifestFile}' was invalid: {message}")]
		static partial void LogCouldNotLoadSdkResolverManifestInvalid (ILogger logger, string manifestFile, string message);

		[LoggerMessage (EventId = 3, Level = LogLevel.Warning, Message = "Empty SDK resolver folder '{resolverFolder}' may indicate corrupt MSBuild installation")]
		static partial void LogEmptySdkResolverFolder (ILogger logger, string resolverFolder);

		[LoggerMessage (EventId = 4, Level = LogLevel.Warning, Message = "Could not load SDK resolver as the manifest '{manifestFile}' refers to missing assembly '{assembly}'")]
		static partial void LogCouldNotLoadSdkResolverMissingAssembly (ILogger logger, string manifestFile, string assembly);

		[LoggerMessage (EventId = 5, Level = LogLevel.Warning, Message = "Could not load SDK resolver as the manifest '{manifestFile}' does not specify an assembly")]
		static partial void LogCouldNotLoadSdkResolverUnspecifiedAssembly (ILogger logger, string manifestFile);
	}
}