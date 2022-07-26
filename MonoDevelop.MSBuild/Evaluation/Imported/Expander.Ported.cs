// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
//  WARNING: THIS FILE WAS IMPORTED FROM MSBUILD
//
//  * IF YOU UPDATE IT, UPDATE THE HASH IN THE URL AND REAPPLY CHANGES FROM THE NOTES IN THIS HEADER
//  * IF YOU MODIFY IT, DESCRIBE THE CHANGES IN THE NOTES
//
//  URL: https://raw.githubusercontent.com/dotnet/msbuild/7434b575d12157ef98aeaad3b86c8f235f551c41/src/Build/Evaluation/Expander.cs
//
//  CHANGES:
//     * This is a subset of Expander.cs that has been ported to be callable from the MonoDevelop.MSBuild evaluator. The original
//       file has been kept beside this one for reference.
//     * All methods have been made static.
//     * Parameters have been added to TryExecuteWellKnownFunction corresponding to the instance fields in the original class,
//       specifically _receiverType/_methodMethodName/_fileSystem

using System;
using System.IO;
using System.Linq;

using Microsoft.Build.FileSystem;

namespace Microsoft.Build.Evaluation
{
    internal class Expander
    {
        public class Function
        {
            /// <summary>
            /// Shortcut to avoid calling into binding if we recognize some most common functions.
            /// Binding is expensive and throws first-chance MissingMethodExceptions, which is
            /// bad for debugging experience and has a performance cost.
            /// A typical binding operation with exception can take ~1.500 ms; this call is ~0.050 ms
            /// (rough numbers just for comparison).
            /// See https://github.com/Microsoft/msbuild/issues/2217.
            /// </summary>
            /// <param name="returnVal">The value returned from the function call.</param>
            /// <param name="objectInstance">Object that the function is called on.</param>
            /// <param name="args">arguments.</param>
            /// <returns>True if the well known function call binding was successful.</returns>
            public static bool TryExecuteWellKnownFunction(
                Type _receiverType, string _methodMethodName, MSBuildFileSystemBase _fileSystem,
                out object returnVal, object objectInstance, object[] args)
            {
                returnVal = null;

                if (objectInstance is string text)
                {
                    if (string.Equals(_methodMethodName, nameof(string.StartsWith), StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryGetArg(args, out string arg0))
                        {
                            returnVal = text.StartsWith(arg0);
                            return true;
                        }
                    }
                    else if (string.Equals(_methodMethodName, nameof(string.Replace), StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryGetArgs(args, out string arg0, out string arg1))
                        {
                            returnVal = text.Replace(arg0, arg1);
                            return true;
                        }
                    }
                    else if (string.Equals(_methodMethodName, nameof(string.Contains), StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryGetArg(args, out string arg0))
                        {
                            returnVal = text.Contains(arg0);
                            return true;
                        }
                    }
                    else if (string.Equals(_methodMethodName, nameof(string.ToUpperInvariant), StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length == 0)
                        {
                            returnVal = text.ToUpperInvariant();
                            return true;
                        }
                    }
                    else if (string.Equals(_methodMethodName, nameof(string.ToLowerInvariant), StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length == 0)
                        {
                            returnVal = text.ToLowerInvariant();
                            return true;
                        }
                    }
                    else if (string.Equals(_methodMethodName, nameof(string.EndsWith), StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryGetArg(args, out string arg0))
                        {
                            returnVal = text.EndsWith(arg0);
                            return true;
                        }
                    }
                    else if (string.Equals(_methodMethodName, nameof(string.ToLower), StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length == 0)
                        {
                            returnVal = text.ToLower();
                            return true;
                        }
                    }
                    else if (string.Equals(_methodMethodName, nameof(string.IndexOf), StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryGetArgs(args, out string arg0, out StringComparison arg1))
                        {
                            returnVal = text.IndexOf(arg0, arg1);
                            return true;
                        }
                    }
                    else if (string.Equals(_methodMethodName, nameof(string.IndexOfAny), StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryGetArg(args, out string arg0))
                        {
                            returnVal = text.IndexOfAny(arg0.ToCharArray());
                            return true;
                        }
                    }
                    else if (string.Equals(_methodMethodName, nameof(string.LastIndexOf), StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryGetArg(args, out string arg0))
                        {
                            returnVal = text.LastIndexOf(arg0);
                            return true;
                        }
                        else if (TryGetArgs(args, out arg0, out int startIndex))
                        {
                            returnVal = text.LastIndexOf(arg0, startIndex);
                            return true;
                        }
                        else if (TryGetArgs(args, out arg0, out StringComparison arg1))
                        {
                            returnVal = text.LastIndexOf(arg0, arg1);
                            return true;
                        }
                    }
                    else if (string.Equals(_methodMethodName, nameof(string.LastIndexOfAny), StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryGetArg(args, out string arg0))
                        {
                            returnVal = text.LastIndexOfAny(arg0.ToCharArray());
                            return true;
                        }
                    }
                    else if (string.Equals(_methodMethodName, nameof(string.Length), StringComparison.OrdinalIgnoreCase))
                    {
                        if (args.Length == 0)
                        {
                            returnVal = text.Length;
                            return true;
                        }
                    }
                    else if (string.Equals(_methodMethodName, nameof(string.Substring), StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryGetArg(args, out int startIndex))
                        {
                            returnVal = text.Substring(startIndex);
                            return true;
                        }
                        else if (TryGetArgs(args, out startIndex, out int length))
                        {
                            returnVal = text.Substring(startIndex, length);
                            return true;
                        }
                    }
                    else if (string.Equals(_methodMethodName, nameof(string.Split), StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryGetArg(args, out string separator) && separator.Length == 1)
                        {
                            returnVal = text.Split(separator[0]);
                            return true;
                        }
                    }
                    else if (string.Equals(_methodMethodName, nameof(string.PadLeft), StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryGetArg(args, out int totalWidth))
                        {
                            returnVal = text.PadLeft(totalWidth);
                            return true;
                        }
                        else if (TryGetArgs(args, out totalWidth, out string paddingChar) && paddingChar.Length == 1)
                        {
                            returnVal = text.PadLeft(totalWidth, paddingChar[0]);
                            return true;
                        }
                    }
                    else if (string.Equals(_methodMethodName, nameof(string.PadRight), StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryGetArg(args, out int totalWidth))
                        {
                            returnVal = text.PadRight(totalWidth);
                            return true;
                        }
                        else if (TryGetArgs(args, out totalWidth, out string paddingChar) && paddingChar.Length == 1)
                        {
                            returnVal = text.PadRight(totalWidth, paddingChar[0]);
                            return true;
                        }
                    }
                    else if (string.Equals(_methodMethodName, nameof(string.TrimStart), StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryGetArg(args, out string trimChars) && trimChars.Length > 0)
                        {
                            returnVal = text.TrimStart(trimChars.ToCharArray());
                            return true;
                        }
                    }
                    else if (string.Equals(_methodMethodName, nameof(string.TrimEnd), StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryGetArg(args, out string trimChars) && trimChars.Length > 0)
                        {
                            returnVal = text.TrimEnd(trimChars.ToCharArray());
                            return true;
                        }
                    }
                    else if (string.Equals(_methodMethodName, "get_Chars", StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryGetArg(args, out int index))
                        {
                            returnVal = text[index];
                            return true;
                        }
                    }
                }
                else if (objectInstance is string[])
                {
                    string[] stringArray = (string[])objectInstance;
                    if (string.Equals(_methodMethodName, "GetValue", StringComparison.OrdinalIgnoreCase))
                    {
                        if (TryGetArg(args, out int index))
                        {
                            returnVal = stringArray[index];
                            return true;
                        }
                    }
                }
                else if (objectInstance == null) // Calling a well-known static function
                {
                    if (_receiverType == typeof(string))
                    {
                        if (string.Equals(_methodMethodName, nameof(string.IsNullOrWhiteSpace), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArg(args, out string arg0))
                            {
                                returnVal = string.IsNullOrWhiteSpace(arg0);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(string.IsNullOrEmpty), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArg(args, out string arg0))
                            {
                                returnVal = string.IsNullOrEmpty(arg0);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(string.Copy), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArg(args, out string arg0))
                            {
                                returnVal = arg0;
                                return true;
                            }
                        }
                    }
                    else if (_receiverType == typeof(Math))
                    {
                        if (string.Equals(_methodMethodName, nameof(Math.Max), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArgs(args, out var arg0, out double arg1))
                            {
                                returnVal = Math.Max(arg0, arg1);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(Math.Min), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArgs(args, out double arg0, out var arg1))
                            {
                                returnVal = Math.Min(arg0, arg1);
                                return true;
                            }
                        }
                    }
                    else if (_receiverType == typeof(IntrinsicFunctions))
                    {
                        if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.EnsureTrailingSlash), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArg(args, out string arg0))
                            {
                                returnVal = IntrinsicFunctions.EnsureTrailingSlash(arg0);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.ValueOrDefault), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArgs(args, out string arg0, out string arg1))
                            {
                                returnVal = IntrinsicFunctions.ValueOrDefault(arg0, arg1);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.NormalizePath), StringComparison.OrdinalIgnoreCase))
                        {
                            if (ElementsOfType(args, typeof(string)))
                            {
                                returnVal = IntrinsicFunctions.NormalizePath(Array.ConvertAll(args, o => (string) o));
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.GetDirectoryNameOfFileAbove), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArgs(args, out string arg0, out string arg1))
                            {
                                returnVal = IntrinsicFunctions.GetDirectoryNameOfFileAbove(arg0, arg1, _fileSystem);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.GetRegistryValueFromView), StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length >= 4 &&
                                TryGetArgs(args, out string arg0, out string arg1, enforceLength: false))
                            {
                                returnVal = IntrinsicFunctions.GetRegistryValueFromView(arg0, arg1, args[2], new ArraySegment<object>(args, 3, args.Length - 3));
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.IsRunningFromVisualStudio), StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length == 0)
                            {
                                returnVal = IntrinsicFunctions.IsRunningFromVisualStudio();
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.Escape), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArg(args, out string arg0))
                            {
                                returnVal = IntrinsicFunctions.Escape(arg0);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.GetPathOfFileAbove), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArgs(args, out string arg0, out string arg1))
                            {
                                returnVal = IntrinsicFunctions.GetPathOfFileAbove(arg0, arg1, _fileSystem);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.Add), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArgs(args, out double arg0, out double arg1))
                            {
                                returnVal = arg0 + arg1;
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.Subtract), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArgs(args, out double arg0, out double arg1))
                            {
                                returnVal = arg0 - arg1;
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.Multiply), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArgs(args, out double arg0, out double arg1))
                            {
                                returnVal = arg0 * arg1;
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.Divide), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArgs(args, out double arg0, out double arg1))
                            {
                                returnVal = arg0 / arg1;
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.GetCurrentToolsDirectory), StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length == 0)
                            {
                                returnVal = IntrinsicFunctions.GetCurrentToolsDirectory();
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.GetToolsDirectory32), StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length == 0)
                            {
                                returnVal = IntrinsicFunctions.GetToolsDirectory32();
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.GetToolsDirectory64), StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length == 0)
                            {
                                returnVal = IntrinsicFunctions.GetToolsDirectory64();
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.GetMSBuildSDKsPath), StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length == 0)
                            {
                                returnVal = IntrinsicFunctions.GetMSBuildSDKsPath();
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.GetVsInstallRoot), StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length == 0)
                            {
                                returnVal = IntrinsicFunctions.GetVsInstallRoot();
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.GetMSBuildExtensionsPath), StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length == 0)
                            {
                                returnVal = IntrinsicFunctions.GetMSBuildExtensionsPath();
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.GetProgramFiles32), StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length == 0)
                            {
                                returnVal = IntrinsicFunctions.GetProgramFiles32();
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.VersionEquals), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArgs(args, out string arg0, out string arg1))
                            {
                                returnVal = IntrinsicFunctions.VersionEquals(arg0, arg1);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.VersionNotEquals), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArgs(args, out string arg0, out string arg1))
                            {
                                returnVal = IntrinsicFunctions.VersionNotEquals(arg0, arg1);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.VersionGreaterThan), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArgs(args, out string arg0, out string arg1))
                            {
                                returnVal = IntrinsicFunctions.VersionGreaterThan(arg0, arg1);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.VersionGreaterThanOrEquals), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArgs(args, out string arg0, out string arg1))
                            {
                                returnVal = IntrinsicFunctions.VersionGreaterThanOrEquals(arg0, arg1);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.VersionLessThan), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArgs(args, out string arg0, out string arg1))
                            {
                                returnVal = IntrinsicFunctions.VersionLessThan(arg0, arg1);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.VersionLessThanOrEquals), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArgs(args, out string arg0, out string arg1))
                            {
                                returnVal = IntrinsicFunctions.VersionLessThanOrEquals(arg0, arg1);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.GetTargetFrameworkIdentifier), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArg(args, out string arg0))
                            {
                                returnVal = IntrinsicFunctions.GetTargetFrameworkIdentifier(arg0);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.GetTargetFrameworkVersion), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArg(args, out string arg0))
                            {
                                returnVal = IntrinsicFunctions.GetTargetFrameworkVersion(arg0);
                                return true;
                            }
                            if (TryGetArgs(args, out string arg1, out int arg2))
                            {
                                returnVal = IntrinsicFunctions.GetTargetFrameworkVersion(arg1, arg2);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.IsTargetFrameworkCompatible), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArgs(args, out string arg0, out string arg1))
                            {
                                returnVal = IntrinsicFunctions.IsTargetFrameworkCompatible(arg0, arg1);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.GetTargetPlatformIdentifier), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArg(args, out string arg0))
                            {
                                returnVal = IntrinsicFunctions.GetTargetPlatformIdentifier(arg0);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.GetTargetPlatformVersion), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArg(args, out string arg0))
                            {
                                returnVal = IntrinsicFunctions.GetTargetPlatformVersion(arg0);
                                return true;
                            }
                            if (TryGetArgs(args, out string arg1, out int arg2))
                            {
                                returnVal = IntrinsicFunctions.GetTargetPlatformVersion(arg1, arg2);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.StableStringHash), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArg(args, out string arg0))
                            {
                                returnVal = IntrinsicFunctions.StableStringHash(arg0);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(IntrinsicFunctions.AreFeaturesEnabled), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArg(args, out Version arg0))
                            {
                                returnVal = IntrinsicFunctions.AreFeaturesEnabled(arg0);
                                return true;
                            }
                        }
                    }
                    else if (_receiverType == typeof(Path))
                    {
                        if (string.Equals(_methodMethodName, nameof(Path.Combine), StringComparison.OrdinalIgnoreCase))
                        {
                            string arg0, arg1, arg2, arg3;

                            // Combine has fast implementations for up to 4 parameters: https://github.com/dotnet/corefx/blob/2c55db90d622fa6279184e6243f0470a3755d13c/src/Common/src/CoreLib/System/IO/Path.cs#L293-L317
                            switch (args.Length)
                            {
                                case 0:
                                    return false;
                                case 1:
                                    if (TryGetArg(args, out arg0))
                                    {
                                        returnVal = Path.Combine(arg0);
                                        return true;
                                    }
                                    break;
                                case 2:
                                    if (TryGetArgs(args, out arg0, out arg1))
                                    {
                                        returnVal = Path.Combine(arg0, arg1);
                                        return true;
                                    }
                                    break;
                                case 3:
                                    if (TryGetArgs(args, out arg0, out arg1, out arg2))
                                    {
                                        returnVal = Path.Combine(arg0, arg1, arg2);
                                        return true;
                                    }
                                    break;
                                case 4:
                                    if (TryGetArgs(args, out arg0, out arg1, out arg2, out arg3))
                                    {
                                        returnVal = Path.Combine(arg0, arg1, arg2, arg3);
                                        return true;
                                    }
                                    break;
                                default:
                                    if (ElementsOfType(args, typeof(string)))
                                    {
                                        returnVal = Path.Combine(Array.ConvertAll(args, o => (string) o));
                                        return true;
                                    }
                                    break;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length == 0)
                            {
                                returnVal = Path.DirectorySeparatorChar;
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(Path.GetFullPath), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArg(args, out string arg0))
                            {
                                returnVal = Path.GetFullPath(arg0);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(Path.IsPathRooted), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArg(args, out string arg0))
                            {
                                returnVal = Path.IsPathRooted(arg0);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(Path.GetTempPath), StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length == 0)
                            {
                                returnVal = Path.GetTempPath();
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(Path.GetFileName), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArg(args, out string arg0))
                            {
                                returnVal = Path.GetFileName(arg0);
                                return true;
                            }
                        }
                        else if (string.Equals(_methodMethodName, nameof(Path.GetDirectoryName), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArg(args, out string arg0))
                            {
                                returnVal = Path.GetDirectoryName(arg0);
                                return true;
                            }
                        }
                    }
                    else if (_receiverType == typeof(Version))
                    {
                        if (string.Equals(_methodMethodName, nameof(Version.Parse), StringComparison.OrdinalIgnoreCase))
                        {
                            if (TryGetArg(args, out string arg0))
                            {
                                returnVal = Version.Parse(arg0);
                                return true;
                            }
                        }
                    }
                    else if (_receiverType == typeof(System.Guid))
                    {
                        if (string.Equals(_methodMethodName, nameof(Guid.NewGuid), StringComparison.OrdinalIgnoreCase))
                        {
                            if (args.Length == 0)
                            {
                                returnVal = Guid.NewGuid();
                                return true;
                            }
                        }
                    }
                }

                return false;
            }

            private static bool ElementsOfType(object[] args, Type type)
            {
                for (var i = 0; i < args.Length; i++)
                {
                    if (args[i].GetType() != type)
                    {
                        return false;
                    }
                }

                return true;
            }

            private static bool TryGetArgs(object[] args, out string arg0, out string arg1, bool enforceLength = true)
            {
                arg0 = null;
                arg1 = null;

                if (enforceLength && args.Length != 2)
                {
                    return false;
                }

                if (args[0] is string value0 &&
                    args[1] is string value1)
                {
                    arg0 = value0;
                    arg1 = value1;

                    return true;
                }

                return false;
            }

            private static bool TryGetArgs(object[] args, out string arg0, out string arg1, out string arg2)
            {
                arg0 = null;
                arg1 = null;
                arg2 = null;

                if (args.Length != 3)
                {
                    return false;
                }

                if (args[0] is string value0 &&
                    args[1] is string value1 &&
                    args[2] is string value2
                    )
                {
                    arg0 = value0;
                    arg1 = value1;
                    arg2 = value2;

                    return true;
                }

                return false;
            }

            private static bool TryGetArgs(object[] args, out string arg0, out string arg1, out string arg2, out string arg3)
            {
                arg0 = null;
                arg1 = null;
                arg2 = null;
                arg3 = null;

                if (args.Length != 4)
                {
                    return false;
                }

                if (args[0] is string value0 &&
                    args[1] is string value1 &&
                    args[2] is string value2 &&
                    args[3] is string value3
                    )
                {
                    arg0 = value0;
                    arg1 = value1;
                    arg2 = value2;
                    arg3 = value3;

                    return true;
                }

                return false;
            }

            private static bool TryGetArgs(object[] args, out string arg0, out string arg1)
            {
                arg0 = null;
                arg1 = null;

                if (args.Length != 2)
                {
                    return false;
                }

                if (args[0] is string value0 &&
                    args[1] is string value1)
                {
                    arg0 = value0;
                    arg1 = value1;

                    return true;
                }

                return false;
            }

            private static bool TryGetArg(object[] args, out int arg0)
            {
                if (args.Length != 1)
                {
                    arg0 = 0;
                    return false;
                }

                return TryConvertToInt(args[0], out arg0);
            }

            private static bool TryGetArg(object[] args, out Version arg0)
            {
                if (args.Length != 1)
                {
                    arg0 = default;
                    return false;
                }

                return TryConvertToVersion(args[0], out arg0);
            }

            private static bool TryConvertToVersion(object value, out Version arg0)
            {
                string val = value as string;

                if (string.IsNullOrEmpty(val) || !Version.TryParse(val, out arg0))
                {
                    arg0 = default;
                    return false;
                }

                return true;
            }

            private static bool TryConvertToInt(object value, out int arg0)
            {
                switch (value)
                {
                    case double d:
                        arg0 = Convert.ToInt32(d);
                        return arg0 == d;
                    case int i:
                        arg0 = i;
                        return true;
                    case string s when int.TryParse(s, out arg0):
                        return true;
                }

                arg0 = 0;
                return false;
            }

            private static bool TryConvertToDouble(object value, out double arg)
            {
                if (value is double unboxed)
                {
                    arg = unboxed;
                    return true;
                }
                else if (value is string str && double.TryParse(str, out arg))
                {
                    return true;
                }

                arg = 0;
                return false;
            }

            private static bool TryGetArg(object[] args, out string arg0)
            {
                if (args.Length != 1)
                {
                    arg0 = null;
                    return false;
                }

                arg0 = args[0] as string;
                return arg0 != null;
            }

            private static bool TryGetArgs(object[] args, out string arg0, out StringComparison arg1)
            {
                if (args.Length != 2)
                {
                    arg0 = null;
                    arg1 = default;

                    return false;
                }

                arg0 = args[0] as string;

                // reject enums as ints. In C# this would require a cast, which is not supported in msbuild expressions
                if (arg0 == null || !(args[1] is string comparisonTypeName) || int.TryParse(comparisonTypeName, out _))
                {
                    arg1 = default;
                    return false;
                }

                // Allow fully-qualified enum, e.g. "System.StringComparison.OrdinalIgnoreCase"
                if (comparisonTypeName.Contains('.'))
                {
                    comparisonTypeName = comparisonTypeName.Replace("System.StringComparison.", "").Replace("StringComparison.", "");
                }

                return Enum.TryParse(comparisonTypeName, out arg1);
            }

            private static bool TryGetArgs(object[] args, out int arg0, out int arg1)
            {
                arg0 = 0;
                arg1 = 0;

                if (args.Length != 2)
                {
                    return false;
                }

                return TryConvertToInt(args[0], out arg0) &&
                       TryConvertToInt(args[1], out arg1);
            }

            private static bool TryGetArgs(object[] args, out double arg0, out double arg1)
            {
                arg0 = 0;
                arg1 = 0;

                if (args.Length != 2)
                {
                    return false;
                }

                return TryConvertToDouble(args[0], out arg0) &&
                       TryConvertToDouble(args[1], out arg1);
            }

            private static bool TryGetArgs(object[] args, out int arg0, out string arg1)
            {
                arg0 = 0;
                arg1 = null;

                if (args.Length != 2)
                {
                    return false;
                }

                arg1 = args[1] as string;
                if (arg1 == null && args[1] is char ch)
                {
                    arg1 = ch.ToString();
                }

                if (TryConvertToInt(args[0], out arg0) &&
                    arg1 != null)
                {
                    return true;
                }

                return false;
            }

            private static bool TryGetArgs(object[] args, out string arg0, out int arg1)
            {
                arg0 = null;
                arg1 = 0;

                if (args.Length != 2)
                {
                    return false;
                }

                var value1 = args[1] as string;
                arg0 = args[0] as string;
                if (value1 != null &&
                    arg0 != null &&
                    int.TryParse(value1, out arg1))
                {
                    return true;
                }

                return false;
            }
        }
    }
}