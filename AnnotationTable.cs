//
// AnnotationTable.cs
//
// Author:
//       Mikayla Hutchinson <m.j.hutchinson@gmail.com>
//
// Copyright (c) 2016 Xamarin Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Linq;
using System.Runtime.CompilerServices;

namespace MonoDevelop.MSBuildEditor
{
	class AnnotationTable<T> where T : class
	{
		//this is optimized for a single value for each key, hence it uses object[] rather than list<object>
		readonly ConditionalWeakTable<T, object []> annotations = new ConditionalWeakTable<T, object []> ();

		public U Get<U> (T o)
		{
			if (!annotations.TryGetValue (o, out object [] values))
				return default (U);
			return values.OfType<U> ().FirstOrDefault ();
		}

		public void Add<U> (T o, U annotation)
		{
			if (Equals (annotation, default (T)))
				return;
			
			if (!annotations.TryGetValue (o, out object [] values)) {
				values = new object [1];
				annotations.Add (o, values);
			} else {
				var idx = Array.FindIndex (values, obj => obj is T);
				if (idx > -1) {
					values [idx] = annotation;
					return;
				}
				Array.Resize (ref values, values.Length + 1);
			}

			values [values.Length - 1] = annotation;
		}
	}

}
