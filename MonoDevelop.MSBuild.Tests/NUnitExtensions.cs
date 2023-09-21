// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.Generic;
using NUnit.Framework.Constraints;

namespace MonoDevelop.MSBuild.Tests
{
	static class NUnitExtensions
	{
		static readonly NUnitEqualityComparer Comparer = new ();

		static bool Compare<T> (T a, T b)
		{
			Tolerance tolerance = Tolerance.Default;
			return Comparer.AreEqual (a, b, ref tolerance);
		}

		// NOTE: this abomination is a result of the fact that CollectionEquivalentConstraint uses a mix of IEnumerable and IEnumerable<T>
		// and therefore compares DictionaryEntry items to KeyValuePair<K,V> in various combinations
		public static CollectionEquivalentConstraint UsingDictionaryComparer<TKey, TValue> (this CollectionEquivalentConstraint constraint)
			=> constraint
				.Using<DictionaryEntry, KeyValuePair<TKey, TValue>> ((a, b) => Compare ((TKey)a.Key, b.Key) && Compare ((TValue)a.Value, b.Value))
				.Using<KeyValuePair<TKey, TValue>, DictionaryEntry> ((a, b) => Compare (a.Key, (TKey)b.Key) && Compare (a.Value, (TValue)b.Value))
				.Using<DictionaryEntry, DictionaryEntry> ((a, b) => Compare ((TKey)a.Key, (TKey)b.Key) && Compare ((TValue)a.Value, (TValue)b.Value))
				.Using<KeyValuePair<TKey, TValue>, KeyValuePair<TKey, TValue>> ((a, b) => Compare (a.Key, b.Key) && Compare (a.Value, b.Value));
	}
}
