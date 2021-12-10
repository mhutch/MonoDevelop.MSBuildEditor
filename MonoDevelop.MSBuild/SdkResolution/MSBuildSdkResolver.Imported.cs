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

using LoggingContext = MonoDevelop.MSBuild.SdkResolution.ILoggingService;
using ElementLocation = MonoDevelop.MSBuild.SdkResolution.ILoggingService;
using Microsoft.Build.BackEnd.SdkResolution;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

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

        internal virtual IList<SdkResolver> LoadResolvers(LoggingContext loggingContext,
            ElementLocation location)
        {
            var resolvers = !String.Equals(IncludeDefaultResolver, "false", StringComparison.OrdinalIgnoreCase) ?
                new List<SdkResolver> {new DefaultSdkResolver(SDKsPath) }
                : new List<SdkResolver>();

            var potentialResolvers = FindPotentialSdkResolvers(
                Path.Combine(ToolsPath32, "SdkResolvers"), location);

            if (potentialResolvers.Count == 0)
            {
                return resolvers;
            }

            foreach (var potentialResolver in potentialResolvers)
            {
                LoadResolvers(potentialResolver, loggingContext, location, resolvers);
            }

            return resolvers.OrderBy(t => t.Priority).ToList();
        }

        /// <summary>
        ///     Find all files that are to be considered SDK Resolvers. Pattern will match
        ///     Root\SdkResolver\(ResolverName)\(ResolverName).dll.
        /// </summary>
        /// <param name="rootFolder"></param>
        /// <param name="location"></param>
        /// <returns></returns>
        internal virtual IList<string> FindPotentialSdkResolvers(string rootFolder, ElementLocation location)
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
                    assemblyAdded = TryAddAssemblyFromManifest(manifest, subfolder.FullName, assembliesList, location);
                }

                if (!assemblyAdded)
                {
					location.LogWarning ("SDK Resolver folder exists but without an SDK Resolver DLL or manifest file. This may indicate a corrupt or invalid installation of MSBuild. SDK resolver path: " + subfolder.FullName);
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

        private bool TryAddAssemblyFromManifest(string pathToManifest, string manifestFolder, List<string> assembliesList, ElementLocation location)
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
					location.LogWarning ($"Could not load SDK Resolver. A manifest file exists, but the path to the SDK Resolver DLL file could not be found. Manifest file path '{pathToManifest}'.");
					return false;
				}

                path = FileUtilities.FixFilePath(manifest.Path);
            }
            catch (XmlException e)
            {
				// Note: Not logging e.ToString() as most of the information is not useful, the Message will contain what is wrong with the XML file.
				location.LogWarning ($"SDK Resolver manifest file is invalid. This may indicate a corrupt or invalid installation of MSBuild. Manifest file path '{pathToManifest}'. Message: {e.Message}");
				return false;
			}

            if (!Path.IsPathRooted(path))
            {
                path = Path.Combine(manifestFolder, path);
                path = Path.GetFullPath(path);
            }

            if (!TryAddAssembly(path, assembliesList))
            {
				location.LogWarning ($"Could not load SDK Resolver. A manifest file exists, but the path to the SDK Resolver DLL file could not be found. Manifest file path '{pathToManifest}'. SDK resolver path: {path}");
				return false;
			}

            return true;
        }

        private bool TryAddAssembly(string assemblyPath, List<string> assembliesList)
        {
            if (string.IsNullOrEmpty(assemblyPath) || !FileUtilities.FileExistsNoThrow(assemblyPath)) return false;

            assembliesList.Add(assemblyPath);
            return true;
        }

        protected virtual IEnumerable<Type> GetResolverTypes(Assembly assembly)
        {
            return assembly.ExportedTypes
                .Select(type => new {type, info = type.GetTypeInfo()})
                .Where(t => t.info.IsClass && t.info.IsPublic && !t.info.IsAbstract && typeof(SdkResolver).IsAssignableFrom(t.type))
                .Select(t => t.type);
        }

        protected virtual Assembly LoadResolverAssembly(string resolverPath, LoggingContext loggingContext, ElementLocation location)
        {
#if !FEATURE_ASSEMBLYLOADCONTEXT
            return Assembly.LoadFrom(resolverPath);
#else
            return s_loader.LoadFromPath(resolverPath);
#endif
        }

        protected virtual void LoadResolvers(string resolverPath, LoggingContext loggingContext, ElementLocation location, List<SdkResolver> resolvers)
        {
            Assembly assembly;
            try
            {
                assembly = LoadResolverAssembly(resolverPath, loggingContext, location);
            }
            catch (Exception e)
            {
				location.LogWarning ($"The SDK resolver assembly \"{resolverPath}\" could not be loaded. {e.Message}");
                return;
            }

            foreach (Type type in GetResolverTypes(assembly))
            {
                try
                {
                    resolvers.Add((SdkResolver)Activator.CreateInstance(type));
                }
                catch (TargetInvocationException e)
                {
                    // .NET wraps the original exception inside of a TargetInvocationException which masks the original message
                    // Attempt to get the inner exception in this case, but fall back to the top exception message
                    string message = e.InnerException?.Message ?? e.Message;

					location.LogWarning ($"The SDK resolver type \"{type.Name}\" failed to load. {e.Message}");
				}
                catch (Exception e)
                {
					location.LogWarning ($"The SDK resolver type \"{type.Name}\" failed to load. {e.Message}");
				}
            }
        }
    }
}