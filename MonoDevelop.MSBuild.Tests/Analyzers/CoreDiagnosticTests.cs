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
	class CoreDiagnosticTests : MSBuildAnalyzerTest
	{
		[Test]
		public void NoImports ()
		{
			var source = @"<Project></Project>";

			var expected = new MSBuildDiagnostic (
				CoreDiagnostics.NoTargets, SpanFromLineColLength (source, 1, 2, 7)
			);

			VerifyDiagnostics (source, null, true, expected);
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

			var expected = new MSBuildDiagnostic (
				CoreDiagnostics.InvalidBool,
				SpanFromLineColLength (source, 3, 15, 9),
				ImmutableDictionary<string, object>.Empty
					.Add ("Name", "bl&ah")
					.Add ("ValueKind", MSBuildValueKind.Bool)
			);

			VerifyDiagnostics (source, null, true, expected);
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
						.Add ("CustomType", customType)
				),
				new MSBuildDiagnostic (
					CoreDiagnostics.UnknownValue,
					SpanFromLineColLength (source, 3, 33, 6),
					ImmutableDictionary<string, object>.Empty
						.Add ("Name", "See Ya")
						.Add ("ValueKind", MSBuildValueKind.CustomType)
						.Add ("CustomType", customType)
				)
			};

			VerifyDiagnostics (source, null, true, false, schema, expected);
		}
	}
}
