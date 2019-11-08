// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

using MonoDevelop.MSBuild.Language.Expressions;

namespace MonoDevelop.MSBuild.Evaluation
{
	[DebuggerDisplay ("{Value} (Multiple:{HasMultipleValues})")]
	struct MSBuildPropertyValue
	{
		bool? isCollapsed;
		IList<ExpressionNode> multiple;

		public MSBuildPropertyValue (ExpressionNode value)
		{
			Value = value;
			multiple = null;
			isCollapsed = null;
		}

		public MSBuildPropertyValue (IList<ExpressionNode> multiple)
		{
			Value = multiple[0];
			this.multiple = multiple.Count > 1 ? multiple : null;
			isCollapsed = null;
		}

		bool CheckCollapsed ()
		{
			if (multiple != null) {
				foreach (var v in multiple) {
					if (v != null && !(v is ExpressionText t && t.IsPure)) {
						return false;
					}
				}
				return true;
			}
			return Value == null || Value is ExpressionText txt && txt.IsPure;
		}

		public static implicit operator MSBuildPropertyValue (string value)
		{
			return new MSBuildPropertyValue (ExpressionParser.Parse (value, ExpressionOptions.ItemsMetadataAndLists));
		}

		public ExpressionNode Value { get; private set; }
		public bool HasMultipleValues => multiple != null;
		public bool IsCollapsed => isCollapsed ?? (isCollapsed = CheckCollapsed ()).Value;
		public IEnumerable<ExpressionNode> GetValues () => multiple ?? throw new InvalidOperationException ();

		internal MSBuildPropertyValue Collapse (IMSBuildEvaluationContext context)
		{
			if (multiple != null) {
				var oldMultiple = multiple;
				multiple = new ExpressionNode[oldMultiple.Count];
				for (int i = 0; i < multiple.Count; i++) {
					multiple[i] = new ExpressionText (0, context.Evaluate (oldMultiple[i]), true);
				}
				Value = multiple[0];
			} else {
				Value = new ExpressionText (0, context.Evaluate (Value), true);
			}
			isCollapsed = true;
			return this;
		}
	}
}