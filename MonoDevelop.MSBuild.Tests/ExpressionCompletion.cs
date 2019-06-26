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
		// params are: document text, typedChar, trigger result, length
		//    typedChar and length can be omitted and default to \0 and zero
		//	  if typedChar is \0, it's treated as explicitly invoking the command
		//    if typedChar is provided, it's added to the document text

		static object[] BareValueTestCases = {
			//explicitly trigger in bare value
			new object[] { "", TriggerState.Value },
			new object[] { "abc", TriggerState.Value, 3 },
			new object[] { "abcde", TriggerState.Value, 5 },
			new object[] { " ", TriggerState.Value, 1 },
			new object[] { "  xyz", TriggerState.Value, 5 },

			//start typing new bare value
			new object[] { "", 'a', TriggerState.Value, 1 },
			new object[] { "", '/', TriggerState.Value, 1 },

			//typing within bare value
			new object[] { "a", 'x', TriggerState.None },
			new object[] { "$", 'x', TriggerState.None },
		};

		static object[] PropertyTestCases = {
			//start typing property
			new object[] { "", '$', TriggerState.Value, 1 },

			//explicit trigger after property start
			new object[] { "$", TriggerState.Value, 1 },

			//auto trigger property name on typing
			new object[] { "$", '(', TriggerState.PropertyName },
			new object[] { "$(", 'a', TriggerState.PropertyName, 1 },

			//explicit trigger in property name
			new object[] { "$(", TriggerState.PropertyName },
			new object[] { "$(abc", TriggerState.PropertyName, 3 },
			new object[] { "$(abcefgh", TriggerState.PropertyName, 7 },

			//explicit trigger after invalid char in property name
			new object[] { "$(a-", TriggerState.None },

			//type char in property name
			new object[] { "$(a", 'b', TriggerState.None },
			new object[] { "$(abc", '$', TriggerState.None },
		};

		static object[] ItemTestCases = {
			//start typing item
			new object[] { "", '@', TriggerState.Value, 1 },

			//explicit trigger after item start
			new object[] { "@", TriggerState.Value, 1 },

			//auto trigger item name on typing
			new object[] { "@", '(', TriggerState.ItemName },
			new object[] { "@(", 'a', TriggerState.ItemName, 1 },

			//explicit trigger in item name
			new object[] { "@(", TriggerState.ItemName },
			new object[] { "@(abc", TriggerState.ItemName, 3 },
			new object[] { "@(abcefgh", TriggerState.ItemName, 7 },

			//explicit trigger after invalid char in item name
			new object[] { "@(a-", TriggerState.None },

			//type char in item name
			new object[] { "@(a", 'b', TriggerState.None },
			new object[] { "@(abc", '$', TriggerState.None },
		};

		static object[] MetadataTestCases = {
			// note metadata allows a surprising amount of whitespace, unlike properties and items
			//start typing metadata
			new object[] { "", '%', TriggerState.Value, 1 },

			//explicit trigger after metadata start
			new object[] { "%", TriggerState.Value, 1 },

			//auto trigger metadata name on typing
			new object[] { "%", '(', TriggerState.MetadataOrItemName },
			new object[] { "%(", 'a', TriggerState.MetadataOrItemName, 1 },
			new object[] { "%(  ", 'a', TriggerState.MetadataOrItemName, 1 },

			//explicit trigger in metadata name
			new object[] { "%(", TriggerState.MetadataOrItemName },
			new object[] { "%(   ", TriggerState.MetadataOrItemName },
			new object[] { "%(abc", TriggerState.MetadataOrItemName, 3 },
			new object[] { "%(  abc", TriggerState.MetadataOrItemName, 3 },
			new object[] { "%(abcefgh", TriggerState.MetadataOrItemName, 7 },

			//explicit trigger after invalid char in metadata name
			new object[] { "%(a-", TriggerState.None },

			//type char in metadata name
			new object[] { "%(a", 'b', TriggerState.None },
			new object[] { "%(abc", '$', TriggerState.None },
		};

		static object[] QualifiedMetadataTestCases = {
			// note metadata allows a surprising amount of whitespace, unlike properties and items

			// explicit trigger qualified metadata name
			new object[] { "%(foo.", TriggerState.MetadataName },
			new object[] { "%(  foo.", TriggerState.MetadataName },
			new object[] { "%(foo .", TriggerState.MetadataName },
			new object[] { "%(foo.ab", TriggerState.MetadataName, 2 },
			new object[] { "%(foo.abcde", TriggerState.MetadataName, 5 },
			new object[] { "%(foo  .abcd", TriggerState.MetadataName, 4 },
			new object[] { "%(foo  .  abc", TriggerState.MetadataName, 3 },

			// automatic trigger qualified metadata name
			new object[] { "%(foo", '.', TriggerState.MetadataName },
			new object[] { "%(foo ", '.', TriggerState.MetadataName },
			new object[] { "%(foo.", 'a', TriggerState.MetadataName, 1 },
			new object[] { "%(foo  .", 'a', TriggerState.MetadataName, 1 },
			new object[] { "%(foo  .  ", 'a', TriggerState.MetadataName, 1 },

			//explicit trigger after invalid char in qualified metadata name
			new object[] { "%(a.b-", TriggerState.None },
			new object[] { "%(a  .b-", TriggerState.None },

			//type char in qualified metadata name
			new object[] { "%(ab.cd", 'e', TriggerState.None },
			new object[] { "%(ab.cd", '$', TriggerState.None },
			new object[] { "%(ab .cd", 'e', TriggerState.None },
			new object[] { "%(ab  .cd", '$', TriggerState.None },
		};

		static object[] PropertyFunctionTestCases = {
			// explicit trigger property function name
			new object[] { "$(foo.", TriggerState.PropertyFunctionName },
			new object[] { "$(foo .", TriggerState.PropertyFunctionName },
			new object[] { "$(foo.ab", TriggerState.PropertyFunctionName, 2 },
			new object[] { "$(foo.abcde", TriggerState.PropertyFunctionName, 5 },
			new object[] { "$(foo  .abcd", TriggerState.PropertyFunctionName, 4 },

			// automatic trigger property function name
			new object[] { "$(foo", '.', TriggerState.PropertyFunctionName },
			new object[] { "$(foo ", '.', TriggerState.PropertyFunctionName },
			new object[] { "$(foo.", 'a', TriggerState.PropertyFunctionName, 1 },
			new object[] { "$(foo  .", 'a', TriggerState.PropertyFunctionName, 1 },

			//explicit trigger after invalid char in property function name
			new object[] { "$(a.b-", TriggerState.None },
			new object[] { "$(a  .b-", TriggerState.None },

			//type char in property function name
			new object[] { "$(ab.cd", 'e', TriggerState.None },
			new object[] { "$(ab.cd", '$', TriggerState.None },
			new object[] { "$(ab .cd", 'e', TriggerState.None },
			new object[] { "$(ab  .cd", '$', TriggerState.None },
		};

		static object[] ItemFunctionTestCases = {
			// explicit trigger item function name
			new object[] { "@(foo->", TriggerState.ItemFunctionName },
			new object[] { "@(foo ->", TriggerState.ItemFunctionName },
			new object[] { "@(foo->ab", TriggerState.ItemFunctionName, 2 },
			new object[] { "@(foo->abcde", TriggerState.ItemFunctionName, 5 },
			new object[] { "@(foo  ->abcd", TriggerState.ItemFunctionName, 4 },

			// automatic trigger item function name
			new object[] { "@(foo-", '>', TriggerState.ItemFunctionName },
			new object[] { "@(foo -", '>', TriggerState.ItemFunctionName },
			new object[] { "@(foo->", 'a', TriggerState.ItemFunctionName, 1 },
			new object[] { "@(foo  ->", 'a', TriggerState.ItemFunctionName, 1 },

			//explicit trigger after invalid char in item function name
			new object[] { "@(a->b/", TriggerState.None },

			//type char in item function name
			new object[] { "@(ab->cd", 'e', TriggerState.None },
			new object[] { "@(ab->cd", '$', TriggerState.None },
			new object[] { "@(ab ->cd", 'e', TriggerState.None },
			new object[] { "@(ab  ->cd", '$', TriggerState.None },
		};

		static object[] StaticPropertyFunctionTestCases = {
			// explicit trigger static property function name
			new object[] { "$([Foo]::", TriggerState.PropertyFunctionName },
			new object[] { "$([Foo]  ::", TriggerState.None }, //space between ] and :: is invalid
			new object[] { "$([Foo]::ab", TriggerState.PropertyFunctionName, 2 },
			new object[] { "$([Foo]::abcde", TriggerState.PropertyFunctionName, 5 },

			// automatic trigger static property function name
			new object[] { "$([Foo]:", ':', TriggerState.PropertyFunctionName },
			new object[] { "$([Foo] :", ':', TriggerState.None }, //space between ] and :: is invalid
			new object[] { "$([Foo]::", 'a', TriggerState.PropertyFunctionName, 1 },
			new object[] { "$([Foo]  ::", 'a', TriggerState.None },

			//explicit trigger after invalid char in static property function name
			new object[] { "$([Foo]::b-", TriggerState.None },
			new object[] { "$([Foo]  ::b-", TriggerState.None },

			//type char in static property function name
			new object[] { "$([Foo]::cd", 'e', TriggerState.None },
			new object[] { "$([Foo]::cd", '$', TriggerState.None },
			new object[] { "$([Foo] ::cd", 'e', TriggerState.None },
			new object[] { "$([Foo]   :: cd", '$', TriggerState.None },
		};

		static object[] StaticPropertyFunctionNameTestCases = {
			//auto trigger static property function class name on typing
			new object[] { "$(", '[', TriggerState.PropertyFunctionClassName },
			new object[] { "$([", 'a', TriggerState.PropertyFunctionClassName, 1 },

			//explicit trigger in static property function class name
			new object[] { "$([", TriggerState.PropertyFunctionClassName },
			new object[] { "$([abc", TriggerState.PropertyFunctionClassName, 3 },
			new object[] { "$([abcefgh", TriggerState.PropertyFunctionClassName, 7 },

			//explicit trigger after invalid char in static property function class name
			new object[] { "$([a-", TriggerState.None },

			//type char in static property function class name
			new object[] { "$([a", 'b', TriggerState.None },
			new object[] { "$([abc", '$', TriggerState.None }
		};

		/*
		new object[] { "$(a[0].", TriggerState.PropertyFunctionName },*/

		/*
		// --- lists ---

		// explicit trigger after list separator
		new object[] { "a,", TriggerState.CommaValue },
		new object[] { "a;", TriggerState.SemicolonValue },

		// automatic trigger after list separator
		new object[] { "a", ',', TriggerState.CommaValue },
		new object[] { "a", ';', TriggerState.SemicolonValue },

		// explicit trigger in value after list separator
		new object[] { "a,b", TriggerState.CommaValue, 1 },
		new object[] { "a;b", TriggerState.SemicolonValue, 1 },
		new object[] { "a,bc", TriggerState.CommaValue, 2 },
		new object[] { "a;bcd", TriggerState.SemicolonValue, 3 },

		//type char in list value
		new object[] { "a,b", TriggerState.CommaValue, 1 },
		new object[] { "a;", TriggerState.SemicolonValue },
		new object[] { "a;b", TriggerState.SemicolonValue, 1 },
		*/

		static object[] PrependExpression (string v, object[] arr)
			=> arr.Select (a => {
				var testCase = (object[])a;
				var newTestCase = new object[testCase.Length];
				newTestCase[0] = v + (string)testCase[0];
				Array.Copy (testCase, 1, newTestCase, 1, testCase.Length - 1);
				return newTestCase;
			}).ToArray ();

		static object[] PropertyFunctionArgumentTestCases = new object[][] {
			PrependExpression ("$(foo.bar('", BareValueTestCases),
			PrependExpression ("$(foo.bar('", PropertyTestCases),
			PrependExpression ("$(foo.bar('", MetadataTestCases),
			PrependExpression ("$(foo.bar('", QualifiedMetadataTestCases),
			PrependExpression ("$(foo.bar(", PropertyTestCases),
			PrependExpression ("$(foo.bar(1, '", BareValueTestCases),
			PrependExpression ("$(foo.bar(1, '", PropertyTestCases),
		}.SelectMany (x => x).ToArray ();

		static object[] ItemFunctionArgumentTestCases = new object[][] {
			PrependExpression ("@(a->'", PropertyTestCases),
			PrependExpression ("@(a->'", MetadataTestCases),
		}.SelectMany (x => x).ToArray ();

		static object[] ExpressionTestCases = new object[][] {
			BareValueTestCases,
			PropertyTestCases,
			ItemTestCases,
			MetadataTestCases,
			QualifiedMetadataTestCases,
			PropertyFunctionTestCases,
			ItemFunctionTestCases,
			StaticPropertyFunctionNameTestCases,
			PropertyFunctionArgumentTestCases,
			ItemFunctionArgumentTestCases
		}.SelectMany (x => x).ToArray ();

		[Test]
		[TestCaseSource ("ExpressionTestCases")]
		public void TestTriggering (object[] args)
		{
			string expr = (string)args[0];
			char typedChar = (args[1] as char?) ?? '\0';
			var expectedState = (TriggerState)(args[1] is char ? args[2] : args[1]);
			int expectedLength = args[args.Length - 1] as int? ?? 0;

			var state = GetTriggerState (
				expr, typedChar, false,
				out int triggerLength, out ExpressionNode triggerNode,
				out IReadOnlyList<ExpressionNode> comparandVariables
			);

			Assert.AreEqual (expectedState, state);
			Assert.AreEqual (expectedLength, triggerLength);
		}

		public void TestCommaListTriggering (object[] args)
		{
		}

		public void TestSemicolonListTriggering (object[] args)
		{
		}
		/*
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
				expr, '\0', true,
				out int triggerLength, out ExpressionNode triggerNode,
				out IReadOnlyList<ExpressionNode> comparandVariables
			);

			Assert.AreEqual (expectedState, state);
			Assert.AreEqual (expectedLength, triggerLength);
			Assert.AreEqual (expectedComparands.Count, comparandVariables?.Count ?? 0);
			for (int i = 0; i < expectedComparands.Count; i++) {
				Assert.AreEqual (expectedComparands[i], ((ExpressionProperty)comparandVariables[i]).Name);
			}
		}*/
	}
}
