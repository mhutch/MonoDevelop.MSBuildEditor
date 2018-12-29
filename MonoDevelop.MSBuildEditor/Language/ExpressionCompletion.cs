// Copyright (c) 2014 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using MonoDevelop.MSBuildEditor.Schema;
using MonoDevelop.Projects.MSBuild.Conditions;
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

		public static bool IsCondition (this MSBuildResolveResult rr)
		{
			return rr.LanguageAttribute != null && rr.LanguageAttribute.ValueKind == MSBuildValueKind.Condition;
		}

		public static TriggerState GetTriggerState (
			string expression, bool isCondition,
			out int triggerLength, out ExpressionNode triggerExpression,
			out IReadOnlyList<ExpressionNode> comparandVariables)
		{
			comparandVariables = null;
			if (isCondition) {
				return GetConditionTriggerState (expression, out triggerLength, out triggerExpression, out comparandVariables);
			}
			return GetTriggerState (expression, out triggerLength, out triggerExpression);
		}

		static TriggerState GetTriggerState (string expression, out int triggerLength, out ExpressionNode triggerExpression)
		{
			triggerLength = 0;

			if (expression.Length == 0) {
				triggerExpression = new ExpressionText (0, "", true);
				return TriggerState.Value;
			}

			if (expression.Length == 1) {
				triggerExpression = new ExpressionText (0, expression, true);
				triggerLength = 1;
				return TriggerState.Value;
			}

			const ExpressionOptions options = ExpressionOptions.ItemsMetadataAndLists | ExpressionOptions.CommaLists;
			triggerExpression = ExpressionParser.Parse (expression, options);

			if (triggerExpression is ExpressionList el) {
				//the last list entry is the thing that triggered it
				triggerExpression = el.Nodes.Last ();
				if (triggerExpression is ExpressionError e && e.Kind == ExpressionErrorKind.EmptyListEntry) {
					return LastChar () == ',' ? TriggerState.CommaValue : TriggerState.SemicolonValue;
				}
				if (triggerExpression is ExpressionText l) {
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

			if (lastNode is ExpressionText lit) {
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
					if (iee.Kind == ExpressionErrorKind.ExpectingMethodOrTransform) {
						return TriggerState.ItemFunctionName;
					}
					if (iee.Kind == ExpressionErrorKind.ExpectingLeftParen) {
						var inv = i.Find (iee.Offset - 1) as ExpressionItemFunctionInvocation;
						if (inv != null && inv.MethodName != null && inv.MethodName.Length == 1) {
							triggerLength = 1;
							return TriggerState.ItemFunctionName;
						}
					}
					break;
				case ExpressionProperty p:
					if (iee.Kind == ExpressionErrorKind.ExpectingRightParenOrPeriod && p.Name.Length == 1) {
						triggerLength = 1;
						return TriggerState.Property;
					}
					if (iee.Kind == ExpressionErrorKind.ExpectingMethodName) {
						return TriggerState.PropertyFunctionName;
					}
					if (iee.Kind == ExpressionErrorKind.ExpectingLeftParen) {
						var inv = p.Find (iee.Offset - 1) as ExpressionPropertyFunctionInvocation;
						if (inv != null && inv.MethodName != null && inv.MethodName.Length == 1) {
							triggerLength = 1;
							return TriggerState.PropertyFunctionName;
						}
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

			return TriggerState.None;

			char LastChar () => expression[expression.Length - 1];
			char PenultimateChar () => expression[expression.Length - 2];
			bool IsPossiblePathSegment (char c) => c == '_' || char.IsLetterOrDigit (c) || c == '.';
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
			DirectorySeparator,
			MetadataOrItem,
			PropertyFunctionName,
			ItemFunctionName,
		}

		public static TriggerState GetConditionTriggerState (
			string expression,
			out int triggerLength, out ExpressionNode triggerExpression,
			out IReadOnlyList<ExpressionNode> comparandValues
		)
		{
			triggerLength = 0;
			triggerExpression = null;
			comparandValues = null;

			if (expression.Length == 0 || (expression.Length == 0 && expression[0] == '\'')) {
				triggerExpression = new ExpressionText (0, "", true);
				return TriggerState.Value;
			}

			if (expression.Length == 1) {
				triggerExpression = new ExpressionText (0, expression, true);
				triggerLength = 1;
				return TriggerState.Value;
			}

			var tokens = new List<Token> ();
			var tokenizer = new ConditionTokenizer ();
			tokenizer.Tokenize (expression);

			int lastExpressionStart = 0;

			while (tokenizer.Token.Type != TokenType.EOF) {
				switch (tokenizer.Token.Type) {
				case TokenType.And:
				case TokenType.Or:
					lastExpressionStart = tokenizer.Token.Position + tokenizer.Token.Value.Length;
					break;
				}
				tokens.Add (tokenizer.Token);
				tokenizer.GetNextToken ();
			}

			int last = tokens.Count - 1;
			if (last >= 2 && TokenIsCondition (tokens[last - 1].Type)) {
				var lt = tokens[last];
				if (lt.Type == TokenType.Apostrophe || (lt.Type == TokenType.String && (expression[lt.Position + lt.Value.Length] != '\''))) {
					lastExpressionStart = lt.Position;
					comparandValues = ReadPrecedingComparandVariables (tokens, last - 2);
				} else {
					triggerLength = 0;
					triggerExpression = null;
					return TriggerState.None;
				}
			}

			var subexpr = expression.Substring (lastExpressionStart);
			return GetTriggerState (subexpr, out triggerLength, out triggerExpression);
		}

		static bool TokenIsCondition (TokenType type)
		{
			switch (type) {
			case TokenType.Equal:
			case TokenType.NotEqual:
			case TokenType.Less:
			case TokenType.LessOrEqual:
			case TokenType.Greater:
			case TokenType.GreaterOrEqual:
				return true;
			default:
				return false;
			}
		}

		//TODO: unqualified metadata
		static IReadOnlyList<ExpressionNode> ReadPrecedingComparandVariables (List<Token> tokens, int index)
		{
			var expr = tokens[index];
			if (expr.Type == TokenType.String) {
				var list = new List<ExpressionNode> ();
				var expression = ExpressionParser.Parse (expr.ToString (), ExpressionOptions.ItemsAndMetadata);
				foreach (var n in expression.WithAllDescendants ()) {
					switch (n) {
					case ExpressionMetadata em:
					case ExpressionProperty ep:
						list.Add (n);
						break;
					}
				}
				return list;
			}

			if (expr.Type == TokenType.RightParen && index - 3 >= 0) {
				if (Readback (1, TokenType.String)) {
					if (Readback (2, TokenType.LeftParen)) {
						if (Readback (3, TokenType.Property)) {
							return new[] { new ExpressionProperty (0, 0, ValueBack (1)) };
						}
						if (Readback (3, TokenType.Metadata)) {
							//TODO: handle unqualified metadata
							return Array.Empty<ExpressionNode> ();
						}
					}
					if (index - 4 >= 0 && Readback (2, TokenType.Dot) && Readback (3, TokenType.String) && Readback (4, TokenType.LeftParen) && Readback (5, TokenType.Metadata)) {
						return new[] { new ExpressionMetadata (0, 0, ValueBack (3), ValueBack (1)) };
					}
				}
			}

			return null;

			bool Readback (int i, TokenType type) => tokens[index - i].Type == type;
			string ValueBack (int i) => tokens[index - i].Value;
		}

		public static IEnumerable<BaseInfo> GetComparandCompletions (MSBuildRootDocument doc, IReadOnlyList<ExpressionNode> variables)
		{
			var names = new HashSet<string> ();
			foreach (var variable in variables) {
				ValueInfo info;
				switch (variable) {
				case ExpressionProperty ep:
					if (ep.IsSimpleProperty) {
						info = doc.GetProperty (ep.Name) ?? new PropertyInfo (ep.Name, null, false);
						break;
					}
					continue;
				case ExpressionMetadata em:
					info = doc.GetMetadata (em.ItemName, em.MetadataName, true) ?? new MetadataInfo (em.MetadataName, null, false);
					break;
				default:
					continue;
				}

				if (info == null) {
					continue;
				}

				IEnumerable<BaseInfo> cinfos;
				if (info.Values != null && info.Values.Count > 0) {
					cinfos = info.Values;
				} else {
					var kind = info.InferValueKindIfUnknown ();
					cinfos = MSBuildCompletionExtensions.GetValueCompletions (kind, doc);
				}

				if (cinfos != null) {
					foreach (var ci in cinfos) {
						if (names.Add (ci.Name)) {
							yield return ci;
						}
					}
				}
			}
		}

		public static IEnumerable<BaseInfo> GetCompletionInfos (
			MSBuildResolveResult rr,
			TriggerState trigger, MSBuildValueKind kind,
			ExpressionNode triggerExpression, int triggerLength,
			MSBuildRootDocument doc)
		{
			switch (trigger) {
			case TriggerState.Value:
				return MSBuildCompletionExtensions.GetValueCompletions (kind, doc, rr);
			case TriggerState.Item:
				return doc.GetItems ();
			case TriggerState.Metadata:
				return doc.GetMetadata (null, true);
			case TriggerState.Property:
				return doc.GetProperties (true);
			case TriggerState.MetadataOrItem:
				return ((IEnumerable<BaseInfo>)doc.GetItems ()).Concat (doc.GetMetadata (null, true));
			case TriggerState.DirectorySeparator:
				return MSBuildCompletionExtensions.GetFilenameCompletions (kind, doc, triggerExpression, triggerLength); ;
			case TriggerState.PropertyFunctionName:
				return FunctionCompletion.GetPropertyFunctionNameCompletions (triggerExpression);
			case TriggerState.ItemFunctionName:
				return FunctionCompletion.GetItemFunctionNameCompletions (triggerExpression);
			}
			throw new InvalidOperationException ();
		}
	}
}
