// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

using MonoDevelop.MSBuild.Language.Expressions;

namespace MonoDevelop.MSBuild.Evaluation
{
	[DebuggerDisplay ("{Value} (Multiple:{HasMultipleValues})")]
	public struct MSBuildPropertyValue
	{
		bool? isCollapsed;
		IList<string> multiple;

		public MSBuildPropertyValue (string value)
		{
			Value = value;
			multiple = null;
			isCollapsed = null;
		}

		public MSBuildPropertyValue (IList<string> multiple)
		{
			Value = multiple[0];
			this.multiple = multiple.Count > 1 ? multiple : null;
			isCollapsed = null;
		}

		static readonly char[] expressionChars = { '@', '$', '%' };
		bool CheckCollapsed ()
		{
			if (multiple != null) {
				foreach (var v in multiple) {
					if (v != null && v.IndexOfAny (expressionChars) > -1) {
						return false;
					}
				}
				return true;
			}
			return Value == null || Value.IndexOfAny (expressionChars) < 0;
		}

		public static implicit operator MSBuildPropertyValue (string value)
		{
			return new MSBuildPropertyValue (value);
		}

		public string Value { get; private set; }
		public bool HasMultipleValues => multiple != null;
		public bool IsCollapsed => isCollapsed ?? (isCollapsed = CheckCollapsed ()).Value;
		public IEnumerable<string> GetValues () => multiple ?? throw new InvalidOperationException ();

		internal MSBuildPropertyValue Collapse (IMSBuildEvaluationContext context)
		{
			if (multiple != null) {
				var oldMultiple = multiple;
				multiple = new string[oldMultiple.Count];
				for (int i = 0; i < multiple.Count; i++) {
					multiple[i] = Collapse (context, oldMultiple[i]);
				}
				Value = multiple[0];
			} else {
				Value = Collapse (context, Value);
			}
			isCollapsed = true;
			return this;
		}

		//FIXME: recursive
		string Collapse (IMSBuildEvaluationContext context, string expression)
		{
			var expr = ExpressionParser.Parse (expression);
			return context.Evaluate (expr);
		}
	}
}