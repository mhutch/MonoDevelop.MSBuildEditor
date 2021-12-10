// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
//  WARNING: THIS FILE WAS IMPORTED FROM MSBUILD
//
//  * IF YOU UPDATE IT, UPDATE THE HASH IN THE URL AND REAPPLY CHANGES FROM THE NOTES IN THIS HEADER
//  * IF YOU MODIFY IT, DESCRIBE THE CHANGES IN THE NOTES
//
//  URL: https://raw.githubusercontent.com/dotnet/msbuild/7434b575d12157ef98aeaad3b86c8f235f551c41/src/Shared/CommunicationsUtilities.cs
//
//  CHANGES: None



using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;

using Microsoft.Build.Shared;
using System.Reflection;
using Microsoft.Build.Utilities;

#if !CLR2COMPATIBILITY
using Microsoft.Build.Shared.Debugging;
#endif
#if !FEATURE_APM
using System.Threading.Tasks;
#endif

namespace Microsoft.Build.Internal
{
    /// <summary>
    /// This class contains utility methods for the MSBuild engine.
    /// </summary>
    static internal class CommunicationsUtilities
    {
        /// <summary>
        /// Gets a hash code for this string.  If strings A and B are such that A.Equals(B), then
        /// they will return the same hash code.
        /// This is as implemented in CLR String.GetHashCode() [ndp\clr\src\BCL\system\String.cs]
        /// but stripped out architecture specific defines
        /// that causes the hashcode to be different and this causes problem in cross-architecture handshaking
        /// </summary>
        internal static int GetHashCode(string fileVersion)
        {
            unsafe
            {
                fixed (char* src = fileVersion)
                {
                    int hash1 = (5381 << 16) + 5381;
                    int hash2 = hash1;

                    int* pint = (int*)src;
                    int len = fileVersion.Length;
                    while (len > 0)
                    {
                        hash1 = ((hash1 << 5) + hash1 + (hash1 >> 27)) ^ pint[0];
                        if (len <= 2)
                        {
                            break;
                        }

                        hash2 = ((hash2 << 5) + hash2 + (hash2 >> 27)) ^ pint[1];
                        pint += 2;
                        len -= 4;
                    }

                    return hash1 + (hash2 * 1566083941);
                }
            }
        }
    }
}