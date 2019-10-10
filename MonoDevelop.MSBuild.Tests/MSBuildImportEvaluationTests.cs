// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MonoDevelop.MSBuild.Evaluation;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.MSBuild.Util;
using MonoDevelop.Xml.Parser;
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

			var results = context.EvaluateWithPermutation (expr).ToList ();
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
		public void TestMicrosoftCSharpTargetsImports ()
		{
			var doc = ParseDoc (
				"<Project><Import Project=\"$(MSBuildToolsPath)\\Microsoft.CSharp.targets\" /></Project>",
				"myfile.csproj"
			);

			AssertImportsExist (
				doc,
				"Microsoft.CSharp.CrossTargeting.targets",
				"Microsoft.CSharp.CurrentVersion.targets",
				"Microsoft.Common.CurrentVersion.targets",
				"NuGet.targets",
				"Microsoft.Common.tasks"
			);
		}

		[Test]
		public void TestNetSdkImports ()
		{
			var doc = ParseDoc (
				"<Project Sdk=\"Microsoft.NET.Sdk\"></Project>",
				"myfile.csproj"
			);

			AssertImportsExist (
				doc,
				//these come from a multivalued import
				"Microsoft.CSharp.CrossTargeting.targets",
				"Microsoft.CSharp.CurrentVersion.targets",
				// this checks we import both VB and C# targets
				"Microsoft.VisualBasic.Core.targets",
				// this comes from a wildcard import
				"NuGet.targets",
				// these are just generally important not to break
				"Microsoft.Common.CurrentVersion.targets",
				"NuGet.Build.Tasks.Pack.targets",
				"Microsoft.NET.Sdk.DefaultItems.props",
				"Microsoft.Common.tasks"
			);

			// check schemas are loaded
			var packageRefItem = doc.GetSchemas ().GetItem ("PackageReference");
			Assert.NotNull (packageRefItem);
			Assert.NotNull (packageRefItem.Description);
			Assert.AreEqual (MSBuildValueKind.NuGetID, packageRefItem.ValueKind);
		}

		static MSBuildRootDocument ParseDoc (string contents, string filename = "myfile.csproj")
		{
			var runtimeInfo = new MSBuildEnvironmentRuntimeInformation ();
			var textSource = new StringTextSource (contents);
			var schemaProvider = new MSBuildSchemaProvider ();
			var taskBuilder = new NoopTaskMetadataBuilder ();

			return MSBuildRootDocument.Parse (textSource, filename, null, schemaProvider, runtimeInfo, taskBuilder, default);
		}

		static void AssertImportsExist (MSBuildRootDocument rootDoc, params string[] filenames)
		{
			var collected = new Dictionary<string, Import> (StringComparer.OrdinalIgnoreCase);
			foreach (var f in filenames) {
				collected.Add (f, null);
			}

			foreach (var import in GetAllImports (rootDoc)) {
				if (import.IsResolved) {
					var name = Path.GetFileName (import.Filename);
					if (collected.ContainsKey (name)) {
						collected[name] = import;
					}
				}
			}

			foreach (var kvp in collected) {
				Assert.NotNull (kvp.Value, "Missing import {0}", kvp.Key);
			}
		}

		static IEnumerable<Import> GetAllImports (MSBuildDocument doc)
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
