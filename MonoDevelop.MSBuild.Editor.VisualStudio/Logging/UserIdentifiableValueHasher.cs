// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Security.Cryptography;
using System.Text;

namespace MonoDevelop.MSBuild.Editor.VisualStudio.Logging;

/// <summary>
/// Used to hash user-identifiable values in telemetry. Not thread safe.
/// </remarks>
class UserIdentifiableValueHasher
{
	const int bufferSize = 4096;
	const bool truncateToBuffer = false;

	readonly HashAlgorithm hashAlgorithm = SHA256.Create ();
	static readonly Encoding encoding = new UTF8Encoding (encoderShouldEmitUTF8Identifier: false);

	public byte[] Hash (string value)
	{
		// we may get an array bigger than requested, and if we do, use it
		var utf8Bytes = ArrayPool<byte>.Shared.Rent (bufferSize);

		try {
			if (truncateToBuffer || encoding.GetMaxByteCount (value.Length) < utf8Bytes.Length || encoding.GetByteCount (value) <= utf8Bytes.Length) {
				int bytesCount = encoding.GetBytes (value, 0, value.Length, utf8Bytes, 0);
				return hashAlgorithm.ComputeHash (utf8Bytes, 0, bytesCount);
			}
		} finally {
			ArrayPool<byte>.Shared.Return (utf8Bytes);
		}

		// our attempt to avoid allocating failed, so just use the allocating version
		utf8Bytes = encoding.GetBytes (value);
		return hashAlgorithm.ComputeHash (utf8Bytes, 0, utf8Bytes.Length);
	}
}