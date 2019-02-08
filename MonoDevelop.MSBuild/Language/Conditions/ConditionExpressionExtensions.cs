// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace MonoDevelop.MSBuild.Language.Conditions
{
	static class ConditionExpressionExtensions
	{
		public static IEnumerable<ConditionExpression> WithAllChildren (ConditionExpression expr)
		{
			yield return expr;
			switch (expr) {
			case ConditionOrExpression o:
				foreach (var c in WithAllChildren (o.Left)) {
					yield return c;
				}
				foreach (var c in WithAllChildren (o.Right)) {
					yield return c;
				}
				break;
			case ConditionAndExpression a:
				foreach (var c in WithAllChildren (a.Left)) {
					yield return c;
				}
				foreach (var c in WithAllChildren (a.Right)) {
					yield return c;
				}
				break;
			case ConditionRelationalExpression r:
				foreach (var c in WithAllChildren (r.Left)) {
					yield return c;
				}
				foreach (var c in WithAllChildren (r.Right)) {
					yield return c;
				}
				break;
			}
		}
	}
}