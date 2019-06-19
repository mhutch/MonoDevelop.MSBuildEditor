// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using MonoDevelop.MSBuild.Evaluation;
using NUnit.Framework;

namespace MonoDevelop.MSBuild.Tests
{
	[TestFixture]
	public class MSBuildImportEvaluationTests
	{
		[Test]
		[TestCase("Hello\\Bye.targets", "Hello\\Bye.targets")]
		[TestCase("$(Foo)", "XfooX")]
		[TestCase("Hello\\$(Foo).targets", "Hello\\XfooX.targets")]
		[TestCase("Hello\\$(Foo).$(Bar)", "Hello\\XfooX.YbarY")]
		public void TestEvaluation (string expr, string expected)
		{
			var context = new TestEvaluationContext {
				{ "Foo", "XfooX" },
				{ "Bar", "YbarY" }
			};

			var evaluated = context.Evaluate (expr);
			Assert.AreEqual (expected, evaluated);
		}
	}

	class TestEvaluationContext : IMSBuildEvaluationContext, IEnumerable
	{
		readonly Dictionary<string, string> properties = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase);

		public void Add (string name, string value)
		{
			properties.Add (name, value);
		}

		IEnumerator IEnumerable.GetEnumerator () => properties.GetEnumerator ();

		public bool TryGetProperty (string name, out MSBuildPropertyValue value)
		{
			if (properties.TryGetValue (name, out var val)) {
				value = val;
				return true;
			}
			value = null;
			return false;
		}
	}
}
