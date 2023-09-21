// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

namespace MonoDevelop.MSBuild
{
	static class CollectionExtensions
	{
		public static ImmutableDictionary<TKey,TValue> AddIfNotNull<TKey, TValue> (this ImmutableDictionary<TKey,TValue> dictionary, TKey key, TValue value)
			where TValue : class
			=> value is null? dictionary : dictionary.Add (key, value);
	}
}
