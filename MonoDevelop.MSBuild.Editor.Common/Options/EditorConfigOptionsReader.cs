// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


using System;
using System.Threading;
using MonoDevelop.Xml.Options;

namespace MonoDevelop.MSBuild.Editor.Options;

/// <summary>
/// Reads options from an editorconfig file
/// </summary>
abstract class EditorConfigOptionsReader : IOptionsReader, IDisposable
{
	public abstract bool TryGetOption<T> (Option<T> option, out T? value);

	/// <summary>
	/// Called when the reader is disposed. Guaranteed to be called exactly once.
	/// </summary>
	/// <param name="disposing">Whether the reader is being disposed explicitly or by the finalizer</param>
	protected virtual void Dispose (bool disposing)
	{
	}

	int disposed = 0;

	public void Dispose ()
	{
		if (Interlocked.Exchange (ref disposed, 1) != 0) {
			return;
		}
		Dispose (true);
		GC.SuppressFinalize (this);
	}

	~EditorConfigOptionsReader ()
	{
		// this might doesn't need to be atomic but let's be extra certain
		if (Interlocked.Exchange (ref disposed, 1) != 0) {
			return;
		}
		Dispose (false);
	}
}