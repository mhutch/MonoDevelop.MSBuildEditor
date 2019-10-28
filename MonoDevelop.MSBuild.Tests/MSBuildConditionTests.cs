// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using MonoDevelop.MSBuild.Language.Expressions;

using NUnit.Framework;

namespace MonoDevelop.MSBuild.Tests
{
	[TestFixture]
    public class MSBuildConditionTests
    {
        [Test]
        public void TestCondition1()
        {
            var condition = @"@(AssemblyAttribute->WithMetadataValue('A', 'B')->Count()) == 0";
			TestParse (condition,
				new ExpressionConditionOperator (0, 63,
					ExpressionOperatorKind.Equal,
					new ExpressionItem (0, 58,
						new ExpressionItemFunctionInvocation (
							2, 55,
							new ExpressionItemFunctionInvocation (2, 46,
								new ExpressionItemName (2, "AssemblyAttribute"),
								new ExpressionFunctionName (21, "WithMetadataValue"),
								new ExpressionArgumentList (38, 10,
									new List<ExpressionNode> {
										new QuotedExpression ('\'', new ExpressionText (40, "A", true)),
										new QuotedExpression ('\'', new ExpressionText (45, "B", true))
									}
								)
							),
							new ExpressionFunctionName (50, "Count"),
							new ExpressionArgumentList (55, 2, new List<ExpressionNode> ())
						)
					),
					new ExpressionArgumentInt (62, 1, 0)
				)
			);
        }

		[Test]
		public void TestNotBool ()
		{
			TestParse (
				" !false ",
				new ExpressionConditionOperator (
					1, 6, ExpressionOperatorKind.Not,
					new ExpressionArgumentBool (2, 5, false),
					null
				)
			);
		}

		[Test]
		public void TestFunctions ()
		{
			TestParse (
				"Exists($(foo)) And !HasTrailingSlash ('$(foobar)')",
				new ExpressionConditionOperator (
					0, 50, ExpressionOperatorKind.And,
					new ExpressionConditionFunction (
						0, 14, "Exists",
						new ExpressionArgumentList (
							6, 8,
							new ExpressionProperty (7, 6, "foo")
						)
					),
					new ExpressionConditionOperator (
						19, 31, ExpressionOperatorKind.Not,
						new ExpressionConditionFunction (
							20, 30, "HasTrailingSlash",
							new ExpressionArgumentList (
								37, 13,
								new QuotedExpression ('\'', new ExpressionProperty (39, 9, "foobar"))
							)
						),
						null
					)
				)
			);
		}

		[Test]
		public void TestPropertyEqualsEmptyString ()
		{
			TestParse (
				"$(foo)==''",
				new ExpressionConditionOperator (
					0, 10, ExpressionOperatorKind.Equal,
					new ExpressionProperty (0, 6, new ExpressionPropertyName (2, 3, "foo")),
					new QuotedExpression ('\'', new ExpressionText (9, "", true))
				)
			);
		}

		static void TestParse (string expression, ExpressionNode expected)
		{
			var expr = ExpressionParser.ParseCondition (expression, 0);
			MSBuildExpressionTests.AssertEqual (expected, expr, 0);

			const int baseOffset = 123;
			expr = ExpressionParser.ParseCondition (expression, baseOffset);
			MSBuildExpressionTests.AssertEqual (expected, expr, baseOffset);
		}
	}
}