// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;

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
				new ExpressionConditionOperator (
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
		public void TestChainedBoolean ()
		{
			TestParse (
				"true and false or !true and !false",
				new ExpressionConditionOperator (ExpressionOperatorKind.And,
					new ExpressionConditionOperator (ExpressionOperatorKind.Or,
						new ExpressionConditionOperator (ExpressionOperatorKind.And,
							new ExpressionArgumentBool (0, true),
							new ExpressionArgumentBool (9, false)
						),
						ExpressionConditionOperator.Not (18, new ExpressionArgumentBool (19, true))
					),
					ExpressionConditionOperator.Not (28, new ExpressionArgumentBool (29, false))
				)
			);
		}

		[Test]
		public void TestCompareAndCompareBoolean ()
		{
			TestParse (
				"$(foo)!='honk' and '$(bar)' >= '' or true",
				new ExpressionConditionOperator (ExpressionOperatorKind.Or,
					new ExpressionConditionOperator (ExpressionOperatorKind.And,
						new ExpressionConditionOperator (ExpressionOperatorKind.NotEqual,
							new ExpressionProperty (0, "foo"),
							new QuotedExpression ('\'', new ExpressionText (9, "honk", true))
						),
						new ExpressionConditionOperator (ExpressionOperatorKind.GreaterThanOrEqual,
							new QuotedExpression ('\'', new ExpressionProperty (20, "bar")),
							new QuotedExpression ('\'', new ExpressionText (32, "", true))
						)
					),
					new ExpressionArgumentBool (37, true)
				)
			);
		}

		[Test]
		public void TestParenGrouping ()
		{
			TestParse (
				"true and ((false or $(foo)=='bar') and !('$(baz.Length)'==5)) and 'thing' != 'other thing'",
				new ExpressionConditionOperator (ExpressionOperatorKind.And,
					new ExpressionConditionOperator (ExpressionOperatorKind.And,
						new ExpressionArgumentBool (0, true),
						new ExpressionParenGroup (9, 52,
							new ExpressionConditionOperator (ExpressionOperatorKind.And,
								new ExpressionParenGroup (10, 24,
									new ExpressionConditionOperator (ExpressionOperatorKind.Or,
										new ExpressionArgumentBool (11, false),
										new ExpressionConditionOperator (ExpressionOperatorKind.Equal,
											new ExpressionProperty (20, "foo"),
											new QuotedExpression ('\'', new ExpressionText (29, "bar", true))
										)
									)
								),
								ExpressionConditionOperator.Not (39,
									new ExpressionParenGroup (40, 20,
										new ExpressionConditionOperator (ExpressionOperatorKind.Equal,
											new QuotedExpression ('\'',
												new ExpressionProperty (42, 13,
													new ExpressionPropertyFunctionInvocation (44, 10,
														new ExpressionPropertyName (44, "baz"),
														new ExpressionFunctionName (48, "Length"),
														null
													)
												)
											),
											new ExpressionArgumentInt (58, 1, 5)
										)
									)
								)
							)
						)
					),
					new ExpressionConditionOperator (ExpressionOperatorKind.NotEqual,
						new QuotedExpression ('\'', new ExpressionText (67, "thing", true)),
						new QuotedExpression ('\'', new ExpressionText (78, "other thing", true))
					)
				)
			);
		}

		[Test]
		[TestCase ("'$(foo)' == '' and ('$(bar)' == '')")] // paren group at end of expression string
		[TestCase ("")] // empty expression
		[TestCase (" ")] // whitespace only
		public void TestParseNoError (string expressionString)
		{
			var expression = ExpressionParser.ParseCondition (expressionString, 0);
			Assert.IsEmpty (expression.WithAllDescendants ().OfType<ExpressionError> ());
		}

		[Test]
		public void TestNotBool ()
		{
			TestParse (
				" !false ",
				ExpressionConditionOperator.Not (1, new ExpressionArgumentBool (2, false))
			);
		}

		[Test]
		public void TestFunctions ()
		{
			TestParse (
				"Exists($(foo)) And !HasTrailingSlash ('$(foobar)')",
				new ExpressionConditionOperator (
					ExpressionOperatorKind.And,
					new ExpressionConditionFunction (
						0, 14, "Exists",
						new ExpressionArgumentList (
							6, 8,
							new ExpressionProperty (7, "foo")
						)
					),
					ExpressionConditionOperator.Not (
						19,
						new ExpressionConditionFunction (
							20, 30, "HasTrailingSlash",
							new ExpressionArgumentList (
								37, 13,
								new QuotedExpression ('\'', new ExpressionProperty (39, "foobar"))
							)
						)
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
					ExpressionOperatorKind.Equal,
					new ExpressionProperty (0, 6, new ExpressionPropertyName (2, "foo")),
					new QuotedExpression ('\'', new ExpressionText (9, "", true))
				)
			);
		}

		[Test]
		public void TestMissingOperand ()
		{
			TestParse (
				"true And",
				new ExpressionConditionOperator (
					ExpressionOperatorKind.And,
					new ExpressionArgumentBool (0, true),
					new ExpressionError (8, ExpressionErrorKind.ExpectingValue, out _)
				)
			);
		}

		[Test]
		public void TestMissingOperandWithSpace ()
		{
			TestParse (
				"true Or  ",
				new ExpressionConditionOperator (
					ExpressionOperatorKind.Or,
					new ExpressionArgumentBool (0, true),
					new ExpressionError (9, ExpressionErrorKind.ExpectingValue, out _)
				)
			);
		}

		[Test]
		public void TestMultiLineCondition ()
		{
			TestParse (
				" '$(ImportWindowsDesktopTargets)' == ''\r\n                            and ('$(UseWpf)' == 'true' Or '$(UseWindowsForms)' == 'true') ",
				new ExpressionConditionOperator (
					ExpressionOperatorKind.And,
					new ExpressionConditionOperator (
						ExpressionOperatorKind.Equal,
						new QuotedExpression ('\'', new ExpressionProperty (2, "ImportWindowsDesktopTargets")),
						new QuotedExpression ('\'', new ExpressionText (38, "", true))
					),
					new ExpressionParenGroup (73, 57,
						new ExpressionConditionOperator (
							ExpressionOperatorKind.Or,
							new ExpressionConditionOperator (
								ExpressionOperatorKind.Equal,
								new QuotedExpression ('\'', new ExpressionProperty (75, "UseWpf")),
								new QuotedExpression ('\'', new ExpressionText (90, "true", true))
							),
							new ExpressionConditionOperator (
								ExpressionOperatorKind.Equal,
								new QuotedExpression ('\'', new ExpressionProperty (100, "UseWindowsForms")),
								new QuotedExpression ('\'', new ExpressionText (124, "true", true))
							)
						)
					)
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