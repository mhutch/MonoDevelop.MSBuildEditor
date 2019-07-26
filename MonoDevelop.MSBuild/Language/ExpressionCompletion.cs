// Copyright (c) 2014 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

using MonoDevelop.MSBuild.Language.Conditions;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Parser;

namespace MonoDevelop.MSBuild.Language
{
	static class ExpressionCompletion
	{
		public static bool IsPossibleExpressionCompletionContext (XmlParser parser)
		{
			//FIXME fragile, should expose this properly somehow
			const int ROOT_STATE_FREE = 0;

			var state = parser.CurrentState;
			return state is XmlAttributeValueState
				|| state is XmlTextState
				|| (state is XmlRootState && ((IXmlParserContext)parser).StateTag == ROOT_STATE_FREE);
		}

		//validates CommaValue and SemicolonValue, and collapses them to Value
		public static bool ValidateListPermitted (ListKind listKind, MSBuildValueKind kind)
		{
			switch (listKind) {
			case ListKind.Comma:
				if (kind.AllowCommaLists ()) {
					return true;
				}
				return false;
			case ListKind.Semicolon:
				if (kind.AllowLists ()) {
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
			string expression, TriggerReason reason, char typedChar, bool isCondition,
			out int triggerLength, out ExpressionNode triggerExpression, out ListKind listKind,
			out IReadOnlyList<ExpressionNode> comparandVariables)
		{
			comparandVariables = null;
			if (isCondition) {
				listKind = ListKind.None;
				return GetConditionTriggerState (expression, reason, typedChar, out triggerLength, out triggerExpression, out comparandVariables);
			}
			return GetTriggerState (expression, reason, typedChar, out triggerLength, out triggerExpression, out listKind);
		}

		static TriggerState GetTriggerState (string expression, TriggerReason reason, char typedChar, out int triggerLength, out ExpressionNode triggerExpression, out ListKind listKind)
		{
			triggerLength = 0;
			listKind = ListKind.None;

			var isExplicit = reason == TriggerReason.Invocation;
			var isNewline = typedChar == '\n';

			if (!isExplicit && !isNewline && expression.Length > 0 && expression[expression.Length - 1] != typedChar) {
				triggerExpression = null;
				LoggingService.LogWarning ($"Expression text '{expression}' is not consistent with typed character '{typedChar}'");
				return TriggerState.None;
			}

			if (expression.Length == 0) {
				//automatically trigger at the start of an expression regardless
				triggerExpression = new ExpressionText (0, expression, true);
				return TriggerState.Value;
			}

			const ExpressionOptions options = ExpressionOptions.ItemsMetadataAndLists | ExpressionOptions.CommaLists;
			triggerExpression = ExpressionParser.Parse (expression, options);

			if (triggerExpression is ListExpression el) {
				//the last list entry is the thing that triggered it
				triggerExpression = el.Nodes.Last ();
				if (triggerExpression is ExpressionError e && e.Kind == ExpressionErrorKind.EmptyListEntry) {
					triggerLength = 0;
					listKind = LastChar () == ',' ? ListKind.Comma : ListKind.Semicolon;
					return TriggerState.Value;
				}
				var separator = expression[triggerExpression.Offset - 1];
				listKind = separator == ',' ? ListKind.Comma : ListKind.Semicolon;
			}

			if (triggerExpression is ExpressionText text) {
				if (!isExplicit && typedChar == '\\') {
					triggerLength = 0;
					return TriggerState.DirectorySeparator;
				}

				var val = text.Value;
				int leadingWhitespace = 0;
				for (int i = 0; i < val.Length; i++) {
					if (char.IsWhiteSpace (val[i])) {
						leadingWhitespace++;
					} else {
						break;
					}
				}

				var length = val.Length - leadingWhitespace;
				if (length == 0) {
					triggerLength = 0;
					return isExplicit ? TriggerState.Value : TriggerState.None;
				}

				var firstChar = val[leadingWhitespace];

				if (length == 1) {
					triggerLength = 1;
					switch (firstChar) {
					case '$': return TriggerState.PropertyOrValue;
					case '@': return TriggerState.ItemOrValue;
					case '%': return TriggerState.MetadataOrValue;
					case '\\':
						triggerLength = 0;
						return TriggerState.DirectorySeparator;
					default: return TriggerState.Value;
					}
				}

				if (isExplicit) {
					var lastSlash = text.Value.LastIndexOf ('\\');
					if (lastSlash != -1) {
						triggerLength = text.Length - lastSlash - 1;
						return TriggerState.DirectorySeparator;
					}
					triggerLength = length;
					return TriggerState.Value;
				}

				triggerLength = 0;
				return TriggerState.None;
			}

			//find the deepest node that touches the end
			var lastNode = triggerExpression.Find (expression.Length);
			if (lastNode == null) {
				return TriggerState.None;
			}

			if (lastNode is ExpressionText lit) {
				if (LastChar () == '\\') {
					return TriggerState.DirectorySeparator;
				}

				//explict trigger grabs back to last slash, if any
				if (isExplicit) {
					var lastSlash = lit.Value.LastIndexOf ('\\');
					if (lastSlash != -1) {
						triggerLength = lit.Length - lastSlash - 1;
						return TriggerState.DirectorySeparator;
					}
				}

				//eager trigger on first char after /
				if (!isExplicit && PenultimateChar () == '\\' && IsPossiblePathSegmentStart (typedChar)) {
					triggerLength = 1;
					return TriggerState.DirectorySeparator;
				}
			}

			//find the deepest error
			var error = lastNode as ExpressionError;
			ExpressionNode parent = lastNode.Parent;
			while (parent != null && error == null) {
				error = parent as ExpressionError;
				parent = parent.Parent;
			}

			if (error is IncompleteExpressionError iee && iee.WasEOF) {
				switch (lastNode) {
				case ExpressionItem i:
					if (iee.Kind == ExpressionErrorKind.ExpectingMethodOrTransform) {
						return TriggerState.ItemFunctionName;
					}
					break;
				case ExpressionItemName ein:
					if (iee.Kind == ExpressionErrorKind.ExpectingRightParenOrDash) {
						if (isExplicit || ein.Name.Length == 1) {
							triggerLength = ein.Name.Length;
							return TriggerState.ItemName;
						}
						return TriggerState.None;
					}
					break;
				case ExpressionPropertyName pn:
					if (iee.Kind == ExpressionErrorKind.ExpectingRightParenOrPeriod) {
						if (isExplicit || pn.Name.Length == 1) {
							triggerLength = pn.Name.Length;
							return TriggerState.PropertyName;
						}
						return TriggerState.None;
					}
					break;
				case ExpressionFunctionName fn:
					if (iee.Kind == ExpressionErrorKind.IncompleteProperty) {
						if (isExplicit || fn.Name.Length == 1) {
							triggerLength = fn.Name.Length;
							return TriggerState.PropertyFunctionName;
						}
						return TriggerState.None;
					}
					if (iee.Kind == ExpressionErrorKind.ExpectingLeftParen) {
						if (isExplicit || fn.Name.Length == 1) {
							triggerLength = fn.Name.Length;
							return TriggerState.ItemFunctionName;
						}
						return TriggerState.None;
					}
					break;
				case ExpressionPropertyFunctionInvocation pfi:
					if (iee.Kind == ExpressionErrorKind.ExpectingMethodName) {
						return TriggerState.PropertyFunctionName;
					}
					if (iee.Kind == ExpressionErrorKind.ExpectingClassName) {
						return TriggerState.PropertyFunctionClassName;
					}
					break;
				case ExpressionClassReference cr:
					if (iee.Kind == ExpressionErrorKind.ExpectingBracketColonColon
						|| ((iee.Kind == ExpressionErrorKind.ExpectingRightParenOrValue || iee.Kind == ExpressionErrorKind.ExpectingRightParenOrComma) && cr.Parent is ExpressionArgumentList)
						) {
						if (isExplicit || cr.Name.Length == 1) {
							triggerLength = cr.Name.Length;
							return cr.Parent is ExpressionArgumentList? TriggerState.BareFunctionArgumentValue : TriggerState.PropertyFunctionClassName;
						}
						return TriggerState.None;
					}
					break;
				case ExpressionMetadata m:
					if (iee.Kind == ExpressionErrorKind.ExpectingMetadataName) {
						return TriggerState.MetadataName;
					}
					if (iee.Kind == ExpressionErrorKind.ExpectingRightParenOrPeriod) {
						if (m.ItemName.Length == 1 || isExplicit) {
							triggerLength = m.ItemName.Length;
							return TriggerState.MetadataOrItemName;
						}
						return TriggerState.None;
					}
					if (iee.Kind == ExpressionErrorKind.ExpectingRightParen) {
						if (m.MetadataName.Length == 1 || isExplicit) {
							triggerLength = m.MetadataName.Length;
							return TriggerState.MetadataName;
						}
						return TriggerState.None;
					}
					break;
				case ExpressionText expressionText: {
						if (
							(error.Kind == ExpressionErrorKind.IncompleteString && (expressionText.Parent is ExpressionArgumentList || expressionText.Parent is ExpressionItemTransform))
							|| (error.Kind == ExpressionErrorKind.ExpectingRightParenOrValue && expressionText.Parent is ExpressionArgumentList)
							) {
							return GetTriggerState (expressionText.Value, reason, typedChar, out triggerLength, out triggerExpression, out _);
						}
					}
					break;
				case ExpressionArgumentList argList: {
						if (error.Kind == ExpressionErrorKind.ExpectingRightParenOrValue) {
							return TriggerState.BareFunctionArgumentValue;
						}
					}
					break;
				}
			}

			if (error != null) {
				switch (error.Kind) {
				case ExpressionErrorKind.ExpectingPropertyName:
					return TriggerState.PropertyName;
				case ExpressionErrorKind.ExpectingItemName:
					return TriggerState.ItemName;
				case ExpressionErrorKind.ExpectingMetadataOrItemName:
					return TriggerState.MetadataOrItemName;
				}
				return TriggerState.None;
			}

			return TriggerState.None;

			char LastChar () => expression[expression.Length - 1];
			char PenultimateChar () => expression[expression.Length - 2];
			bool IsPossiblePathSegmentStart (char c) => c == '_' || char.IsLetterOrDigit (c) || c == '.';
		}

		public enum TriggerState
		{
			None,
			Value,
			ItemName,
			PropertyName,
			MetadataName,
			DirectorySeparator,
			MetadataOrItemName,
			PropertyFunctionName,
			ItemFunctionName,
			PropertyFunctionClassName,
			/// <summary>Value prefiltered to metadata</summary>
			MetadataOrValue,
			/// <summary>Value prefiltered to item</summary>
			ItemOrValue,
			/// <summary>Value prefiltered to property</summary>
			PropertyOrValue,
			/// <summary>Bare function argument</summary>
			BareFunctionArgumentValue,
		}

		public enum TriggerReason
		{
			Invocation,
			TypedChar,
			Backspace
		}

		public enum ListKind
		{
			None,
			Comma,
			Semicolon
		}

		public static TriggerState GetConditionTriggerState (
			string expression,
			TriggerReason reason, char typedChar,
			out int triggerLength, out ExpressionNode triggerExpression,
			out IReadOnlyList<ExpressionNode> comparandValues
		)
		{
			triggerLength = 0;
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
			return GetTriggerState (subexpr, reason, typedChar, out triggerLength, out triggerExpression, out _);
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
			MSBuildRootDocument doc, IFunctionTypeProvider functionTypeProvider)
		{
			switch (trigger) {
			case TriggerState.Value:
			case TriggerState.MetadataOrValue:
			case TriggerState.ItemOrValue:
			case TriggerState.PropertyOrValue:
				return MSBuildCompletionExtensions.GetValueCompletions (kind, doc, rr, triggerExpression);
			case TriggerState.ItemName:
				return doc.GetItems ();
			case TriggerState.MetadataName:
				return doc.GetMetadata (null, true);
			case TriggerState.PropertyName:
				return doc.GetProperties (true);
			case TriggerState.MetadataOrItemName:
				return ((IEnumerable<BaseInfo>)doc.GetItems ()).Concat (doc.GetMetadata (null, true));
			case TriggerState.DirectorySeparator:
				return MSBuildCompletionExtensions.GetFilenameCompletions (kind, doc, triggerExpression, triggerLength, rr);
			case TriggerState.PropertyFunctionName:
				return functionTypeProvider.GetPropertyFunctionNameCompletions (triggerExpression);
			case TriggerState.ItemFunctionName:
				return functionTypeProvider.GetItemFunctionNameCompletions ();
			case TriggerState.PropertyFunctionClassName:
				return functionTypeProvider.GetClassNameCompletions ();
			case TriggerState.None:
				break;
			case TriggerState.BareFunctionArgumentValue:
				//FIXME: enum completion etc
				return MSBuildValueKind.Bool.GetSimpleValues (true);
			}
			throw new InvalidOperationException ($"Unhandled trigger type {trigger}");
		}
	}
}
