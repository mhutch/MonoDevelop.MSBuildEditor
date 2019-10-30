// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Microsoft.CodeAnalysis
{
    /// <summary>
    /// Shared object pool for roslyn
    /// 
    /// Use this shared pool if only concern is reducing object allocations.
    /// if perf of an object pool itself is also a concern, use ObjectPool directly.
    /// 
    /// For example, if you want to create a million of small objects within a second, 
    /// use the ObjectPool directly. it should have much less overhead than using this.
    /// </summary>
    internal static class SharedPools
    {
		// byte pooled memory : 4K * 512 = 4MB
		public const int ByteBufferSize = 4 * 1024;
		private const int ByteBufferCount = 512;

		/// <summary>
		/// Used to reduce the # of temporary byte[]s created to satisfy serialization and
		/// other I/O requests
		/// </summary>
		public static readonly ObjectPool<byte[]> ByteArray = new ObjectPool<byte[]>(() => new byte[ByteBufferSize], ByteBufferCount);
    }
}
