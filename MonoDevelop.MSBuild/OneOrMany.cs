// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;

namespace MonoDevelop.MSBuild
{
	/// <summary>
	/// Represents a value that may have a single value or multiple values.
	/// This avoids allocations for the (common) case of a single value.
	/// Consumers can treat it as an enumerable regardless whether it has multiple values or not.
	/// Alternatively, they can treat it as a single value, ignoring all but the first value.
	/// </summary>
	/// <typeparam name="T">The type of the value</typeparam>
	public struct OneOrMany<T> : IEnumerable<T>
	{
		readonly T one;
		readonly IList<T> many;
		public OneOrMany (T one)
		{
			this.one = one;
			many = null;
		}

		public OneOrMany (IList<T> many)
		{
			if (many.Count == 0) {
				throw new ArgumentException (nameof (many));
			}
			one = many[0];
			this.many = many.Count > 1? many : null;
		}

		public static implicit operator OneOrMany<T> (T one) => new (one);
		public static implicit operator OneOrMany<T> (T[] many) => new (many);
		public static explicit operator T (OneOrMany<T> oom) => oom.one;

		public T First => one;

		public int Count => many?.Count ?? 1;
		public bool IsMany => many is not null;

		IEnumerator<T> YieldSingleValue () { yield return one; }

		public IEnumerator<T> GetEnumerator () => (many as IEnumerable<T>)?.GetEnumerator () ?? YieldSingleValue ();

		IEnumerator IEnumerable.GetEnumerator () => GetEnumerator ();
	}
}
