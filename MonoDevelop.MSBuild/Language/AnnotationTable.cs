// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace MonoDevelop.MSBuild.Language
{
	class AnnotationTable<T> where T : class
	{
		readonly ConditionalWeakTable<T, List<object>> annotations = new ConditionalWeakTable<T, List<object>> ();

		public U Get<U> (T o)
		{
			if (!annotations.TryGetValue (o, out List<object> values))
				return default (U);
			return values.OfType<U> ().FirstOrDefault ();
		}

		public IEnumerable<U> GetMany<U> (T o)
		{
			if (!annotations.TryGetValue (o, out List<object> values))
				return Array.Empty<U> ();
			return values.OfType<U> ();
		}

		public void Add<U> (T o, U annotation)
		{
			if (Equals (annotation, default (T)))
				return;

			if (!annotations.TryGetValue (o, out List<object> values)) {
				values = new List<object> ();
				annotations.Add (o, values);
			}
			values.Add (annotation);
		}
	}

}
