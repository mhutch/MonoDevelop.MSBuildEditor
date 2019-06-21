// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MonoDevelop.MSBuild.Evaluation;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.MSBuild.Util;
using NUnit.Framework;

namespace MonoDevelop.MSBuild.Tests
{
	[TestFixture]
	public class MSBuildImportEvaluationTests
	{
		[OneTimeSetUp]
		public void LoadMSBuild () => MSBuildTestHelpers.RegisterMSBuildAssemblies ();

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


		[Test]
		public void TestRecursiveEvaluation ()
		{
			var context = new TestEvaluationContext {
				{ "Foo", "$(Bar)" },
				{ "Bar", "Hello $(Baz)" },
				{ "Baz", "World" }
			};

			var evaluated = context.Evaluate ("$(Foo)");
			Assert.AreEqual ("Hello World", evaluated);
		}

		[Test]
		public void TestEndlessRecursiveEvaluation ()
		{
			var context = new TestEvaluationContext {
				{ "Foo", "$(Bar)" },
				{ "Bar", "Hello $(Baz)" },
				{ "Baz", "$(Foo)" }
			};

			Assert.Throws<Exception> (() => context.Evaluate ("$(Foo)"));
		}

		[Test]
		[TestCase ("$(Foo)", "One", "Two", "Three")]
		[TestCase ("$(Foo) Thing", "One Thing", "Two Thing", "Three Thing")]
		[TestCase ("X$(Foo)X", "XOneX", "XTwoX", "XThreeX")]
		[TestCase ("$(Bar)", "Hello X", "Hello Y")]
		public void TestPermutedEvaluation (object[] args)
		{
			var expr = (string)args[0];

			var context = new TestEvaluationContext {
				{ "Foo", new MSBuildPropertyValue (new[] { "One", "Two", "Three" }) },
				{ "Bar", "Hello $(Baz)" },
				{ "Baz", new MSBuildPropertyValue (new[] { "X", "Y" }) }
			};

			var results = context.EvaluateWithPermutation (null, ExpressionParser.Parse (expr), 0).ToList ();
			Assert.AreEqual (args.Length - 1, results.Count);
			for (int i = 0; i < args.Length - 1; i++) {
				Assert.AreEqual (args[i+1], results[i]);
			}
		}

		[Test]
		public void PropertyCollectorEvaluationContextTest ()
		{
			var collector = new PropertyValueCollector (false);
			collector.Mark ("Hello");
			collector.Collect ("Hello", "One");
			collector.Collect ("Hello", "Two");

			var ctx = new MSBuildCollectedValuesEvaluationContext (
				new TestEvaluationContext (),
				collector
			);

			var vals = ctx.EvaluateWithPermutation ("$(Hello)").ToList ();
			Assert.AreEqual (2, vals.Count);
			Assert.AreEqual ("One", vals[0]);
			Assert.AreEqual ("Two", vals[1]);
		}
		[Test]
		public void TestCommonTargetsImports ()
		{
			var runtimeInfo = new MSBuildEnvironmentRuntimeInformation ();
			var textSource = TextSourceFactory.CreateNewDocument (
				"<Project><Import Project=\"$(MSBuildToolsPath)\\Microsoft.CSharp.targets\" /></Project>",
				"myfile.csproj"
			);
			var schemaProvider = new MSBuildSchemaProvider ();

			var rootDoc = MSBuildRootDocument.Parse (textSource, null, schemaProvider, runtimeInfo, default);
			var imports = GetAllImports (rootDoc).ToList ();
			Console.WriteLine (imports.Count);

			IEnumerable<Import> GetAllImports (MSBuildDocument doc)
			{
				foreach (var import in doc.Imports) {
					yield return import;
					if (import.IsResolved) {
						foreach (var childImport in GetAllImports (import.Document)) {
							yield return childImport;
						}
					}
				}
			}
		}

	}

	class TestEvaluationContext : IMSBuildEvaluationContext, IEnumerable
	{
		readonly Dictionary<string, MSBuildPropertyValue> properties
			= new Dictionary<string, MSBuildPropertyValue> (StringComparer.OrdinalIgnoreCase);

		public void Add (string name, MSBuildPropertyValue value)
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
