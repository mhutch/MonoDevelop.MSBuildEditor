// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using MonoDevelop.MSBuild.Evaluation;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Language.Typesystem;
using MonoDevelop.MSBuild.Schema;
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
			Assert.AreEqual (expected, evaluated.EscapedValue);
		}

		[Test]
		[TestCase("$([MSBuild]::NormalizePath('Foo','Bar'))", "@Foo\\Bar")]
		[TestCase("X $([MSBuild]::NormalizePath('Foo','Bar'))Y", "X @Foo\\BarY")]
		[TestCase("$([MSBuild]::NormalizePath('Foo','Bar', \"Baz\"))", "@Foo\\Bar\\Baz")]
		[TestCase("$([System.IO.Path]::Combine('Foo', \"Bar\", 'Baz.cs'))", "Foo\\Bar\\Baz.cs")]
		[TestCase("$([System.IO.Path]::Combine('$(Foo)', \"Bar\", 'Baz.cs'))", "World\\Bar\\Baz.cs")]
		public void TestPropertyFunctionEvaluation (string expr, string expected)
		{
			var workingDir = Environment.CurrentDirectory;
			if (workingDir[workingDir.Length - 1] != Path.DirectorySeparatorChar) {
				workingDir += Path.DirectorySeparatorChar;
			}
			expected = expected.Replace ("@", workingDir);

			var context = new TestEvaluationContext {
				{ "Foo", "World" }
			};

			var evaluated = context.Evaluate (expr);
			Assert.AreEqual (expected, evaluated.EscapedValue);
		}

		[Test]
		[TestCase ("$(Foo)", "One", "Two", "Three")]
		[TestCase ("$(Foo) Thing", "One Thing", "Two Thing", "Three Thing")]
		[TestCase ("X$(Foo)X", "XOneX", "XTwoX", "XThreeX")]
		[TestCase ("$(Foo) $(Bar)", "One X", "One Y", "Two X", "Two Y", "Three X", "Three Y")]
		public void TestPermutedEvaluation (object[] args)
		{
			var expr = (string)args[0];

			var context = new TestEvaluationContext {
				{ "Foo", new[] { "One", "Two", "Three" } },
				{ "Bar", new[] { "X", "Y" } }
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
			var noopEvalCtx = new TestEvaluationContext ();
			collector.Mark ("Hello");
			collector.Collect (noopEvalCtx, "Hello", new ExpressionText (0, "One", true));
			collector.Collect (noopEvalCtx, "Hello", new ExpressionText (0, "Two", true));

			var ctx = new MSBuildCollectedValuesEvaluationContext (
				noopEvalCtx,
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
			Assert.AreEqual (MSBuildValueKind.NuGetID.AsList (), packageRefItem.ValueKind);
		}

		[Test]
		public void TestImportWithSdk ()
		{
			var doc = ParseDoc (
				"<Project><Import Project=\"Sdk.props\" Sdk=\"Microsoft.NET.Sdk\" /></Project>",
				"myfile.csproj"
			);

			AssertImportsExist (
				doc,
				"Microsoft.NET.Sdk.DefaultItems.props"
			);
		}

		static MSBuildRootDocument ParseDoc (string contents, string filename = "myfile.csproj")
		{
			var runtimeInfo = new CurrentProcessMSBuildEnvironment ();
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
		readonly Dictionary<string, OneOrMany<EvaluatedValue>> properties = new (StringComparer.OrdinalIgnoreCase);

		public void Add (string name, string[] evaluatedValues)
		{
			properties.Add (name, new OneOrMany<EvaluatedValue> (Array.ConvertAll (evaluatedValues, v => new EvaluatedValue (v))));
		}

		public void Add (string name, string evaluatedValue)
		{
			properties.Add (name, new EvaluatedValue (evaluatedValue));
		}

		IEnumerator IEnumerable.GetEnumerator () => properties.GetEnumerator ();

		public bool TryGetProperty (string name, [NotNullWhen (true)] out EvaluatedValue? value)
		{
			if (properties.TryGetValue (name, out var values)) {
				value = values.First;
				return true;
			}
			value = null;
			return false;
		}

		public bool TryGetMultivaluedProperty (string name, [NotNullWhen (true)] out OneOrMany<EvaluatedValue>? value, bool isProjectImportStart = false)
		{
			if (properties.TryGetValue (name, out var values)) {
				value = values;
				return true;
			}
			value = null;
			return false;
		}
	}
}
