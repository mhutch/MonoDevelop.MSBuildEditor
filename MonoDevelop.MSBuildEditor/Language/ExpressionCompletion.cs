// Copyright (c) 2014 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using MonoDevelop.MSBuildEditor.Schema;
using MonoDevelop.Projects.Formats.MSBuild.Conditions;
using MonoDevelop.Xml.Parser;

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
		public static bool ValidateListPermitted (ref TriggerState triggerState, MSBuildValueKind kind)
		{
			switch (triggerState) {
			case TriggerState.CommaValue:
				if (kind.AllowCommaLists ()) {
					triggerState = TriggerState.Value;
					return true;
				}
				return false;
			case TriggerState.SemicolonValue:
				if (kind.AllowLists ()) {
					triggerState = TriggerState.Value;
					return true;
				}
				return false;
			default:
				return true;
			}
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

			// trigger on '
			if (lastChar == '\'' && OddQuotes (expression)) {
				return TriggerState.QuoteValue;
			}

			//trigger on letter after '
			if (expression.Length >= 2 && expression [expression.Length - 2] == '\'' && char.IsLetter (lastChar) && OddQuotes (expression)) {
				return TriggerState.QuoteValue;
			}

			return TriggerState.None;
		}

		static bool OddQuotes (string s)
		{
			bool odd = false;
			foreach (char c in s) {
				if (c == '\'') {
					odd = !odd;
				}
			}
			return odd;
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
			QuoteValue
		}

		public static IEnumerable<BaseInfo> GetCompletionInfos (
			TriggerState trigger, MSBuildValueKind kind,
			IEnumerable<IMSBuildSchema> schemas,
			IReadOnlyList<FrameworkReference> tfms)
		{
			switch (trigger) {
			case TriggerState.Value:
				return MSBuildCompletionExtensions.GetValueCompletions (kind, schemas, tfms);
			case TriggerState.Item:
				return schemas.GetItems ();
			case TriggerState.Metadata:
				return schemas.GetMetadata (null, true);
			case TriggerState.Property:
				return schemas.GetProperties (true);
			}
			throw new InvalidOperationException ();
		}

		public static IEnumerable<BaseInfo> GetConditionValueCompletion (
			MSBuildResolveResult rr, string expression,
			IEnumerable<IMSBuildSchema> schemas,
			IReadOnlyList<FrameworkReference> tfms)
		{
			if (rr.LanguageAttribute == null || rr.LanguageAttribute.ValueKind != MSBuildValueKind.Condition) {
				yield break;
			}

			var tokens = new List<Token> ();
			var tokenizer = new ConditionTokenizer ();
			tokenizer.Tokenize (expression);
			while (tokenizer.Token.Type != TokenType.EOF) {
				tokens.Add (tokenizer.Token);
				tokenizer.GetNextToken ();
			}

			if (tokens.Count < 3) {
				yield break;
			}

			//check we're starting a value
			var last = tokens [tokens.Count - 1];
			if (last.Type != TokenType.Apostrophe && (last.Type != TokenType.String || last.Value.Length > 0)) {
				yield break;
			}

			//check it was preceded by a comparision
			var penultimate = tokens [tokens.Count - 2];
			switch (penultimate.Type) {
			case TokenType.Equal:
			case TokenType.NotEqual:
			case TokenType.Less:
			case TokenType.LessOrEqual:
			case TokenType.Greater:
			case TokenType.GreaterOrEqual:
				break;
			default:
				yield break;
			}

			var variables = ReadPrecedingComparandVariables (tokens, tokens.Count - 3, schemas);

			foreach (var variable in variables) {
				if (variable != null) {
					IEnumerable<BaseInfo> cinfos;
					if (variable.Values != null && variable.Values.Count > 0) {
						cinfos = variable.Values;
					} else {
						cinfos = MSBuildCompletionExtensions.GetValueCompletions (variable.ValueKind, schemas, tfms);
					}
					if (cinfos != null) {
						foreach (var ci in cinfos) {
							yield return ci;
						}
					}
					continue;

				}
			}
		}

		//TODO: unqualified metadata
		static IEnumerable<ValueInfo> ReadPrecedingComparandVariables (List<Token> tokens, int index, IEnumerable<IMSBuildSchema> schemas)
		{
			var expr = tokens [index];
			if (expr.Type == TokenType.String) {
				var expression = ExpressionParser.Parse (expr.ToString (), ExpressionOptions.ItemsAndMetadata);
				foreach (var n in expression.WithAllDescendants ()) {
					switch (n) {
					case ExpressionProperty ep:
						var pinfo = schemas.GetProperty (ep.Name);
						if (pinfo != null) {
							yield return pinfo;
						}
						break;
					case ExpressionMetadata em:
						var itemName = em.GetItemName ();
						if (itemName != null) {
							var minfo = schemas.GetMetadata (itemName, em.MetadataName, true);
							if (minfo != null) {
								yield return minfo;
							}
						}
						break;
					}
				}
			}

			if (expr.Type == TokenType.RightParen && index - 3 >= 0) {
				if (Readback (1, TokenType.String)) {
					if (Readback (2, TokenType.LeftParen)){
						if (Readback (3, TokenType.Property)) {
							var info = schemas.GetProperty (ValueBack (1));
							if (info != null) {
								yield return info;
							}
							yield break;
						}
						if (Readback (3, TokenType.Metadata)) {
							//TODO: handle unqualified metadata
							yield break;
						}
					}
					if (index - 4 >= 0 && Readback (2, TokenType.Dot) && Readback (3, TokenType.String) && Readback (4, TokenType.LeftParen) && Readback (5, TokenType.Metadata)) {
						var info = schemas.GetMetadata (ValueBack (3), ValueBack (1), true);
						if (info != null) {
							yield return info;
						}
					}
				}
			}

			bool Readback (int i, TokenType type) => tokens [index - i].Type == type;
			string ValueBack (int i) => tokens [index - i].Value;
		}
	}
}
