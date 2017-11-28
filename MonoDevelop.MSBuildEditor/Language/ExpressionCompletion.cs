// Copyright (c) 2014 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
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
		public static TriggerState GetTriggerState (string expression, out int triggerLength, out ExpressionNode triggerExpression)
		{
			triggerLength = 0;

			if (expression.Length == 0) {
				triggerExpression = new ExpressionLiteral (0, "", true);
				return TriggerState.Value;
			}

			if (expression.Length == 1) {
				triggerExpression = new ExpressionLiteral (0, expression, true);
				triggerLength = 1;
				return TriggerState.Value;
			}

			const ExpressionOptions options = ExpressionOptions.ItemsMetadataAndLists | ExpressionOptions.CommaLists;
			triggerExpression = ExpressionParser.Parse (expression, options);

			if (triggerExpression is ExpressionList el) {
				//the last list entry is the thing that triggered it
				triggerExpression = el.Nodes.Last ();
				if (triggerExpression is ExpressionError e && e.Kind == ExpressionErrorKind.EmptyListEntry) {
					return LastChar () == ','? TriggerState.CommaValue : TriggerState.SemicolonValue;
				}
				if (triggerExpression is ExpressionLiteral l) {
					if (l.Length == 1) {
						triggerLength = 1;
						return PenultimateChar () == ',' ? TriggerState.CommaValue : TriggerState.SemicolonValue;
					}
				}
			}

			var lastNode = triggerExpression;
			if (lastNode is Expression expr) {
				lastNode = expr.Nodes.Last ();
			}

			if (lastNode is ExpressionLiteral lit) {
				if (LastChar () == '\\') {
					return TriggerState.DirectorySeparator;
				}

				if (lit.Value.Length >= 2 && PenultimateChar () == '\\' && IsPossiblePathSegment (LastChar ())) {
					triggerLength = 1;
					return TriggerState.DirectorySeparator;
				}
			}

			if (lastNode is IncompleteExpressionError iee && iee.WasEOF) {
				switch (iee.IncompleteNode) {
				case ExpressionItem i:
					if (iee.Kind == ExpressionErrorKind.ExpectingRightParenOrDash && i.Name.Length == 1) {
						triggerLength = 1;
						return TriggerState.Item;
					}
					break;
				case ExpressionProperty p:
					if (iee.Kind == ExpressionErrorKind.ExpectingRightParen && p.Name.Length == 1) {
						triggerLength = 1;
						return TriggerState.Property;
					}
					break;
				case ExpressionMetadata m:
					if (iee.Kind == ExpressionErrorKind.ExpectingMetadataName) {
						return TriggerState.Metadata;
					}
					if (iee.Kind == ExpressionErrorKind.ExpectingRightParenOrPeriod && m.ItemName.Length == 1) {
						triggerLength = 1;
						return TriggerState.MetadataOrItem;
					}
					if (iee.Kind == ExpressionErrorKind.ExpectingRightParen && m.MetadataName.Length == 1) {
						triggerLength = 1;
						return TriggerState.Metadata;
					}
					break;
				}
				return TriggerState.None;
			}

			if (lastNode is ExpressionError err) {
				switch (err.Kind) {
				case ExpressionErrorKind.ExpectingPropertyName:
					return TriggerState.Property;
				case ExpressionErrorKind.ExpectingItemName:
					return TriggerState.Item;
				case ExpressionErrorKind.ExpectingMetadataOrItemName:
					return TriggerState.MetadataOrItem;
				}
				return TriggerState.None;
			}

			// trigger on '
			if (LastChar () == '\'' && OddQuotes (expression)) {
				return TriggerState.QuoteValue;
			}

			//trigger on letter after '
			if (expression.Length >= 2 && PenultimateChar () == '\'' && char.IsLetter (LastChar ()) && OddQuotes (expression)) {
				return TriggerState.QuoteValue;
			}
			return TriggerState.None;

			char LastChar () => expression [expression.Length - 1];
			char PenultimateChar () => expression [expression.Length - 2];
			bool IsPossiblePathSegment (char c) => c == '_' || char.IsLetterOrDigit (c) || c == '.';
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
			QuoteValue,
			DirectorySeparator,
			MetadataOrItem
		}

		public static IEnumerable<BaseInfo> GetCompletionInfos (
			TriggerState trigger, MSBuildValueKind kind,
			ExpressionNode triggerExpression, int triggerLength,
			MSBuildRootDocument doc)
		{
			switch (trigger) {
			case TriggerState.Value:
				return MSBuildCompletionExtensions.GetValueCompletions (kind, doc);
			case TriggerState.Item:
				return doc.GetItems ();
			case TriggerState.Metadata:
				return doc.GetMetadata (null, true);
			case TriggerState.Property:
				return doc.GetProperties (true);
			case TriggerState.MetadataOrItem:
				return ((IEnumerable<BaseInfo>)doc.GetItems ()).Concat (doc.GetMetadata (null, true));
			case TriggerState.DirectorySeparator:
				return MSBuildCompletionExtensions.GetFilenameCompletions (kind, doc, triggerExpression, triggerLength);;
			}
			throw new InvalidOperationException ();
		}

		public static IEnumerable<BaseInfo> GetConditionValueCompletion (
			MSBuildResolveResult rr, string expression,
			MSBuildRootDocument doc)
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

			var variables = ReadPrecedingComparandVariables (tokens, tokens.Count - 3, doc);

			foreach (var variable in variables) {
				if (variable != null) {
					IEnumerable<BaseInfo> cinfos;
					if (variable.Values != null && variable.Values.Count > 0) {
						cinfos = variable.Values;
					} else {
						cinfos = MSBuildCompletionExtensions.GetValueCompletions (variable.ValueKind, doc);
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
