// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Linq;

using MonoDevelop.MSBuild.Analysis;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Typesystem;
using MonoDevelop.MSBuild.Schema;

using NUnit.Framework;

namespace MonoDevelop.MSBuild.Tests.Analyzers
{
	[TestFixture]
	class CoreDiagnosticTests : MSBuildDocumentTest
	{
		[Test]
		public void NoImports ()
		{
			var source = @"<Project></Project>";

			var expected = new MSBuildDiagnostic (
				CoreDiagnostics.NoTargets, SpanFromLineColLength (source, 1, 2, 7)
			);

			VerifyDiagnostics (
				source,
				includeCoreDiagnostics: true,
				expectedDiagnostics: [ expected ],
				includeNoTargetsWarning: true
			);
		}

		[Test]
		public void InvalidBool ()
		{
			// the invalid value has whitespace around it and uses an entity, so we can test the position and length of the error
			var source = @"<Project>
<PropertyGroup>
	<EnableFoo>  bl&amp;ah  </EnableFoo>
</PropertyGroup>
</Project>";

			var schema = new MSBuildSchema {
				new PropertyInfo ("EnableFoo", "", MSBuildValueKind.Bool)
			};

			var expected = new MSBuildDiagnostic (
				CoreDiagnostics.InvalidBool,
				SpanFromLineColLength (source, 3, 15, 9),
				ImmutableDictionary<string, object>.Empty
					.Add ("Name", "bl&ah")
					.Add ("ValueKind", MSBuildValueKind.Bool),
				messageArgs: ["bl&ah"]
			);

			VerifyDiagnostics (source, out _,
				includeCoreDiagnostics: true,
				expectedDiagnostics: [expected],
				schema: schema);
		}


		[Test]
		public void InvalidCustomTypeListValue ()
		{
			var source = @"<Project>
<PropertyGroup>
	<Greetings>  Hello ; Bye;Hi  ; See Ya</Greetings>
</PropertyGroup>
</Project>";

			var schema = new MSBuildSchema ();

			var customType = new CustomTypeInfo (new[] { "Hello", "Hi", "Welcome" }.Select (s => new CustomTypeValue (s, "")).ToList ());
			var greetingsProp = new PropertyInfo ("Greetings", "", valueKind: MSBuildValueKind.CustomType.AsList (), customType: customType);
			schema.Properties.Add (greetingsProp.Name, greetingsProp);

			var expected = new[] {
				new MSBuildDiagnostic (
					CoreDiagnostics.UnknownValue,
					SpanFromLineColLength (source, 3, 23, 3),
					ImmutableDictionary<string, object>.Empty
						.Add ("Name", "Bye")
						.Add ("ValueKind", MSBuildValueKind.CustomType)
						.Add ("CustomType", customType),
					messageArgs: [ "Property", "Greetings", "Bye" ]
				),
				new MSBuildDiagnostic (
					CoreDiagnostics.UnknownValue,
					SpanFromLineColLength (source, 3, 33, 6),
					ImmutableDictionary<string, object>.Empty
						.Add ("Name", "See Ya")
						.Add ("ValueKind", MSBuildValueKind.CustomType)
						.Add ("CustomType", customType),
					messageArgs: ["Property", "Greetings", "See Ya" ]
				)
			};

			VerifyDiagnostics (source, out _,
				includeCoreDiagnostics: true,
				ignoreUnexpectedDiagnostics: false,
				schema: schema,
				expectedDiagnostics: expected
			);
		}

		[TestCase("net48", null)]
		[TestCase("net8.0-android", null)]
		[TestCase("blah5.1", CoreDiagnostics.UnknownTargetFramework_Id)]
		[TestCase("net404.0", CoreDiagnostics.TargetFrameworkHasUnknownVersion_Id)]
		[TestCase("net7.0-potato", CoreDiagnostics.TargetFrameworkHasUnknownTargetPlatform_Id)]
		// FIXME: NuGetFramework parses all these successfully even though they're malformed
		[TestCase("---", CoreDiagnostics.UnknownTargetFramework_Id)]
		[TestCase("net4.8-", CoreDiagnostics.UnknownTargetFramework_Id)]
		// FIXME: we don't have a set of known versions yet
		//[TestCase("net7.0-ios404", CoreDiagnostics.TargetFrameworkHasUnknownTargetPlatformVersion_Id)]
		public void TargetFrameworkError (string tfm, string diagnosticId)
		{
			// the invalid value has whitespace around it and uses an entity, so we can test the position and length of the error
			var source = @"<Project Sdk=""Microsoft.NET.Sdk"">
<PropertyGroup>
	<TargetFramework>  {0}  </TargetFramework>
</PropertyGroup>
</Project>";

			source = string.Format (source, tfm);

			var schema = new MSBuildSchema {
				new PropertyInfo ("TargetFramework", "", MSBuildValueKind.TargetFramework)
			};

			var diagnostics = GetDiagnostics (source, out _, schema: schema, includeCoreDiagnostics: true);

			if (diagnosticId is null) {
				Assert.AreEqual (0, diagnostics.Count);
				return;
			}

			Assert.AreEqual (1, diagnostics.Count);
			var diag = diagnostics[0];

			Assert.AreEqual (SpanFromLineColLength (source, 3, 21, tfm.Length), diag.Span);
			Assert.AreEqual (diagnosticId, diag.Descriptor.Id);
		}


		[Test]
		public void UnknownCultureName ()
		{
			var source = @"<Project>
<PropertyGroup>
	<Cultures>en-US;qps-ploc;nope-no;pt-BR;fr;xx-yy</Cultures>
</PropertyGroup>
</Project>";

			var schema = new MSBuildSchema ();

			var culturesProp = new PropertyInfo ("Cultures", "", valueKind: MSBuildValueKind.Culture.AsList ());
			schema.Properties.Add (culturesProp.Name, culturesProp);

			var expected = new[] {
				new MSBuildDiagnostic (
					CoreDiagnostics.InvalidCulture,
					SpanFromLineColLength (source, 3, 27, 7),
					messageArgs: [ "nope-no" ]
				),
				new MSBuildDiagnostic (
					CoreDiagnostics.UnknownCulture,
					SpanFromLineColLength (source, 3, 44, 5),
					messageArgs: [ "xx-yy" ]
				)
			};

			VerifyDiagnostics (source, out _,
				includeCoreDiagnostics: true,
				schema: schema,
				expectedDiagnostics: expected
			);
		}
	}
}
