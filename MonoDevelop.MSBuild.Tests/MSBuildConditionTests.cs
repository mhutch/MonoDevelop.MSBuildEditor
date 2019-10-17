// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.Projects.MSBuild.Conditions;

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
            var expression = ConditionParser.ParseCondition(condition);
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
		public void TestPropertyEqualsEmptyString ()
		{
			TestParse (
				"$(foo)==''",
				new ExpressionConditionOperator (
					0, 10, ExpressionOperatorKind.Equal,
					new ExpressionProperty (0, 6, new ExpressionPropertyName (2, 3, "foo")),
					new ExpressionText (9, "", true)
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