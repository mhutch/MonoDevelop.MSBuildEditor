// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using MonoDevelop.MSBuild.Language;
using NUnit.Framework;
using static MonoDevelop.MSBuild.Language.ExpressionCompletion;
using System.Linq;
using System;
using MonoDevelop.MSBuild.Language.Expressions;

namespace MonoDevelop.MSBuild.Tests
{
	[TestFixture]
	class ExpressionCompletion
	{
		[Test]
		[TestCase ("", TriggerState.Value, 0)]
		[TestCase ("ax", TriggerState.None, 0)]
		[TestCase ("a", TriggerState.Value, 1)]
		[TestCase ("$", TriggerState.Value, 1)]
		[TestCase ("$x", TriggerState.None, 0)]
		[TestCase ("$(", TriggerState.Property, 0)]
		[TestCase ("$(a", TriggerState.Property, 1)]
		[TestCase ("$(a-", TriggerState.None, 0)]
		[TestCase ("@", TriggerState.Value, 1)]
		[TestCase ("@x", TriggerState.None, 0)]
		[TestCase ("@(", TriggerState.Item, 0)]
		[TestCase ("@(a", TriggerState.Item, 1)]
		[TestCase ("%", TriggerState.Value, 1)]
		[TestCase ("%x", TriggerState.None, 0)]
		[TestCase ("%(", TriggerState.MetadataOrItem, 0)]
		[TestCase ("%(a", TriggerState.MetadataOrItem, 1)]
		[TestCase ("%(a-", TriggerState.None, 0)]
		[TestCase ("%(a.", TriggerState.Metadata, 0)]
		[TestCase ("%(a.b", TriggerState.Metadata, 1)]
		[TestCase ("a,", TriggerState.CommaValue, 0)]
		[TestCase ("a,b", TriggerState.CommaValue, 1)]
		[TestCase ("a;", TriggerState.SemicolonValue, 0)]
		[TestCase ("a;b", TriggerState.SemicolonValue, 1)]
		[TestCase ("$(a.", TriggerState.PropertyFunctionName, 0)]
		[TestCase ("$(a.b", TriggerState.PropertyFunctionName, 1)]
		[TestCase ("$(a.  b", TriggerState.PropertyFunctionName, 1)]
		[TestCase ("@(a->", TriggerState.ItemFunctionName, 0)]
		[TestCase ("@(a->b", TriggerState.ItemFunctionName, 1)]
		[TestCase ("@(a->  b", TriggerState.ItemFunctionName, 1)]
		[TestCase ("$([Foo]::", TriggerState.PropertyFunctionName, 0)]
		[TestCase ("$([Foo]::a", TriggerState.PropertyFunctionName, 1)]
		[TestCase ("$([Foo]:: a", TriggerState.PropertyFunctionName, 1)]
		[TestCase ("$([", TriggerState.PropertyFunctionClassName, 0)]
		[TestCase ("$([a", TriggerState.PropertyFunctionClassName, 1)]
		[TestCase ("$([ a", TriggerState.PropertyFunctionClassName, 1)]
		[TestCase ("$(foo.bar($(", TriggerState.Property, 0)]
		[TestCase ("$(foo.bar($(a", TriggerState.Property, 1)]
		[TestCase ("$(foo.bar('$(", TriggerState.Property, 0)]
		[TestCase ("$(foo.bar('$(a", TriggerState.Property, 1)]
		[TestCase ("$(foo.bar('%(", TriggerState.MetadataOrItem, 0)]
		[TestCase ("$(foo.bar('%(a", TriggerState.MetadataOrItem, 1)]
		[TestCase ("$(foo.bar(1, '$(", TriggerState.Property, 0)]
		[TestCase ("$(foo.bar(1, '$(a", TriggerState.Property, 1)]
		[TestCase ("@(a->'$(", TriggerState.Property, 0)]
		[TestCase ("@(a->'$(b", TriggerState.Property, 1)]
		[TestCase ("@(a->'$(b)','$(a", TriggerState.Property, 1)]
		[TestCase ("@(a->'%(", TriggerState.MetadataOrItem, 0)]
		[TestCase ("@(a->'%(b", TriggerState.MetadataOrItem, 1)]
		[TestCase ("$(a[0].", TriggerState.PropertyFunctionName, 0)]
		[TestCase ("foo,", TriggerState.CommaValue, 0)]
		[TestCase ("foo,a", TriggerState.CommaValue, 1)]
		[TestCase ("foo;", TriggerState.SemicolonValue, 0)]
		[TestCase ("foo;a", TriggerState.SemicolonValue, 1)]
		public void TestTriggering (string expr, TriggerState expectedState, int expectedLength)
		{
			var state = GetTriggerState (
				expr, false,
				out int triggerLength, out ExpressionNode triggerNode,
				out IReadOnlyList<ExpressionNode> comparandVariables
			);
			Assert.AreEqual (expectedState, state);
			Assert.AreEqual (expectedLength, triggerLength);
		}

		[TestCase ("", TriggerState.Value, 0)]
		[TestCase ("$(", TriggerState.Property, 0)]
		[TestCase ("$(Foo) == '", TriggerState.Value, 0, "Foo")]
		[TestCase ("$(Foo) == '$(", TriggerState.Property, 0, "Foo")]
		[TestCase ("$(Foo) == '$(a", TriggerState.Property, 1, "Foo")]
		[TestCase ("$(Foo) == 'a", TriggerState.Value, 1, "Foo")]
		[TestCase ("'$(Foo)' == 'a", TriggerState.Value, 1, "Foo")]
		[TestCase ("'$(Foo)|$(Bar)' == 'a", TriggerState.Value, 1, "Foo", "Bar")]
		[TestCase ("$(Foo) == 'a'", TriggerState.None, 0)]
		[TestCase ("$(Foo) == 'a' And $(Bar) >= '", TriggerState.Value, 0, "Bar")]
		public void TestConditionTriggering (params object[] args)
		{
			string expr = (string)args[0];
			var expectedState = (TriggerState)args[1];
			int expectedLength = (int)args[2];
			var expectedComparands = args.Skip (3).Cast<string> ().ToList ();

			var state = GetTriggerState (
				expr, true,
				out int triggerLength, out ExpressionNode triggerNode,
				out IReadOnlyList<ExpressionNode> comparandVariables
			);

			Assert.AreEqual (expectedState, state);
			Assert.AreEqual (expectedLength, triggerLength);
			Assert.AreEqual (expectedComparands.Count, comparandVariables?.Count ?? 0);
			for (int i = 0; i < expectedComparands.Count; i++) {
				Assert.AreEqual (expectedComparands[i], ((ExpressionProperty)comparandVariables[i]).Name);
			}
		}
	}
}
