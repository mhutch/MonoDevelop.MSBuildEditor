//
// MSBuildEvaluationContext.cs
//
// Author:
//       Lluis Sanchez Gual <lluis@xamarin.com>
//
// Copyright (c) 2014 Xamarin, Inc (http://www.xamarin.com)
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
using BF = System.Reflection.BindingFlags;
using System.Reflection;

namespace MonoDevelop.MSBuildEditor
{
	class MSBuildEvaluationContext
	{
		object wrapped;
		MethodInfo evaluateMeth, setPropMeth, getPropMeth;
		
		public MSBuildEvaluationContext ()
		{
			var type = typeof (MonoDevelop.Projects.MSBuild.MSBuildItem).Assembly.GetType ("MonoDevelop.Projects.MSBuild.MSBuildEvaluationContext", true);
			evaluateMeth = type.GetMethod ("Evaluate", BF.Instance | BF.NonPublic | BF.Public, null, CallingConventions.Any, new [] { typeof (string) }, null );
			setPropMeth = type.GetMethod ("SetPropertyValue", BF.Instance | BF.NonPublic | BF.Public, null, CallingConventions.Any, new [] { typeof (string), typeof (string) }, null);
			getPropMeth = type.GetMethod ("GetPropertyValue", BF.Instance | BF.NonPublic | BF.Public, null, CallingConventions.Any, new [] { typeof (string) }, null);
			wrapped = Activator.CreateInstance (type);
		}

		internal string Evaluate (string import)
		{
			return (string) evaluateMeth.Invoke (wrapped, new [] { import });
		}

		internal void SetPropertyValue (string name, string value)
		{
			setPropMeth.Invoke (wrapped, new [] { name, value});
		}

		internal string GetPropertyValue (string name)
		{
			return (string) getPropMeth.Invoke (wrapped, new [] { name });
		}
	}
}