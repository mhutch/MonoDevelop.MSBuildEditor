// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.MSBuildEditor.Language;
using NUnit.Framework;
using static MonoDevelop.MSBuildEditor.Language.ExpressionCompletion;

namespace MonoDevelop.MSBuildEditor.Tests
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
		public void TestTriggering (string expr, TriggerState expectedState, int expectedLength)
		{
			var state = GetTriggerState (expr, out int triggerLength, out ExpressionNode triggerNode);
			Assert.AreEqual (expectedState, state);
			Assert.AreEqual (expectedLength, triggerLength);
		}
	}
}
