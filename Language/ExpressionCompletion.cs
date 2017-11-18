// Copyright (c) 2014 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.Xml.Parser;
using MonoDevelop.MSBuildEditor.Schema;
using System.Linq;
using System.Collections.Generic;
using System;

namespace MonoDevelop.MSBuildEditor.Language
{
	static class ExpressionCompletion
	{
		public static bool IsPossibleExpressionCompletionContext (XmlParser parser)
		{
			//FIXME fragile, should expose this properly somehow
			const int ROOT_STATE_FREE = 0;

			var state = parser.CurrentState;
			return state is XmlAttributeValueState
				|| (state is XmlRootState && ((IXmlParserContext)parser).StateTag == ROOT_STATE_FREE);
		}

		//validates CommaValue and SemicolonValue, and collapses them to Value
		public static bool ValidateListPermitted (ref TriggerState triggerState, VariableInfo info)
		{
			if (triggerState != TriggerState.CommaValue && triggerState != TriggerState.SemicolonValue) {
				return true;
			}

			if (info.ValueSeparators != null) {
				char ch = triggerState == TriggerState.CommaValue ? ',' : ';';
				if (info.ValueSeparators.Contains (ch)) {
					triggerState = TriggerState.Value;
					return true;
				}
				return false;
			}

			if (triggerState == TriggerState.SemicolonValue && info.ValueKind.AllowLists ()) {
				triggerState = TriggerState.Value;
				return true;
			}

			return false;
		}

		//FIXME: This is very rudimentary. We should parse the expression for real.
		public static TriggerState GetTriggerState (string expression, out int triggerLength)
		{
			triggerLength = 0;

			if (expression.Length == 0) {
				return TriggerState.Value;
			}

			if (expression.Length == 1) {
				triggerLength = 1;
				return TriggerState.Value;
			}

			char lastChar = expression [expression.Length - 1];

			if (lastChar == ',') {
				return TriggerState.CommaValue;
			}

			if (lastChar == ';') {
				return TriggerState.SemicolonValue;
			}

			//trigger on letter after $(, @(
			if (expression.Length >= 3 && char.IsLetter (lastChar) && expression [expression.Length - 2] == '(') {
				char c = expression [expression.Length - 3];
				switch (c) {
				case '$':
					triggerLength = 1;
					return TriggerState.Property;
				case '@':
					triggerLength = 1;
					return TriggerState.Item;
				case '%':
					triggerLength = 1;
					return TriggerState.Metadata;
				}
			}

			//trigger on $(, @(
			if (expression [expression.Length - 1] == '(') {
				char c = expression [expression.Length - 2];
				switch (c) {
				case '$':
					return TriggerState.Property;
				case '@':
					return TriggerState.Item;
				case '%':
					return TriggerState.Metadata;
				}
			}

			return TriggerState.None;
		}

		public enum TriggerState
		{
			None,
			Value,
			SemicolonValue,
			CommaValue,
			Item,
			Property,
			Metadata,
		}

		public static IEnumerable<BaseInfo> GetCompletionInfos (TriggerState trigger, MSBuildValueKind valueKind, IEnumerable<IMSBuildSchema> schemas)
		{
			switch (trigger) {
			case TriggerState.Value:
				return MSBuildCompletionExtensions.GetValueCompletions (valueKind);
			case TriggerState.Item:
				return schemas.GetItems ();
			case TriggerState.Metadata:
				return schemas.GetMetadata (null, true);
			case TriggerState.Property:
				return schemas.GetProperties (true);
			}
			throw new InvalidOperationException ();
		}
	}
}
