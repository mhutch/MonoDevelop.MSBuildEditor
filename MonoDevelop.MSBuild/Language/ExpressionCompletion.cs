// Copyright (c) 2014 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Parser;

namespace MonoDevelop.MSBuild.Language
{
	static class ExpressionCompletion
	{
		public static bool IsPossibleExpressionCompletionContext (XmlSpineParser parser)
			=> parser.IsInAttributeValue () || parser.IsInText () || parser.IsRootFree ();

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

			var state = GetTriggerState (expression, reason, typedChar, isCondition, out triggerLength, out triggerExpression, out var triggerNode, out listKind);

			if (state != TriggerState.None && isCondition) {
				comparandVariables = GetComparandVariables (triggerNode);
			}

			return state;
		}

		static TriggerState GetTriggerState (
			string expression, TriggerReason reason, char typedChar, bool isCondition,
			out int triggerLength, out ExpressionNode triggerExpression, out ExpressionNode triggerNode, out ListKind listKind)
		{
			triggerLength = 0;
			listKind = ListKind.None;

			var isExplicit = reason == TriggerReason.Invocation;
			var isNewline = typedChar == '\n';
			var isBackspace = reason == TriggerReason.Backspace;
			var isTypedChar = reason == TriggerReason.TypedChar;

			if (isTypedChar && !isNewline && expression.Length > 0 && expression[expression.Length - 1] != typedChar) {
				triggerExpression = null;
				triggerNode = null;
				LoggingService.LogWarning ($"Expression text '{expression}' is not consistent with typed character '{typedChar}'");
				return TriggerState.None;
			}

			if (expression.Length == 0) {
				//automatically trigger at the start of an expression regardless
				triggerExpression = new ExpressionText (0, expression, true);
				triggerNode = triggerExpression;
				return TriggerState.Value;
			}

			if (isCondition) {
				triggerExpression = ExpressionParser.ParseCondition (expression);
			} else {
				const ExpressionOptions options = ExpressionOptions.ItemsMetadataAndLists | ExpressionOptions.CommaLists;
				triggerExpression = ExpressionParser.Parse (expression, options);
			}

			if (triggerExpression is ListExpression el) {
				//the last list entry is the thing that triggered it
				triggerExpression = el.Nodes.Last ();
				if (triggerExpression is ExpressionError e && e.Kind == ExpressionErrorKind.EmptyListEntry) {
					triggerLength = 0;
					listKind = LastChar () == ',' ? ListKind.Comma : ListKind.Semicolon;
					triggerNode = triggerExpression;
					return TriggerState.Value;
				}
				var separator = expression[triggerExpression.Offset - 1];
				listKind = separator == ',' ? ListKind.Comma : ListKind.Semicolon;
			}

			if (triggerExpression is ExpressionText text) {
				if (typedChar == '\\' || (!isTypedChar && LastChar () == '\\')) {
					triggerLength = 0;
					triggerNode = triggerExpression;
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
					triggerNode = triggerExpression;
					return isExplicit ? TriggerState.Value : TriggerState.None;
				}

				//auto trigger on first char
				if (length == 1 && !isBackspace) {
					triggerLength = 1;
					triggerNode = triggerExpression;
					return TriggerState.Value;
				}

				if (isExplicit) {
					var lastSlash = text.Value.LastIndexOf ('\\');
					if (lastSlash != -1) {
						triggerLength = text.Length - lastSlash - 1;
					triggerNode = triggerExpression;
						return TriggerState.DirectorySeparator;
					}
					triggerLength = length;
					triggerNode = triggerExpression;
					return TriggerState.Value;
				}

				triggerLength = 0;
				triggerNode = null;
				return TriggerState.None;
			}

			//find the deepest node that touches the end
			triggerNode = triggerExpression.Find (expression.Length);
			if (triggerNode == null) {
				return TriggerState.None;
			}

			if (triggerNode is ExpressionText lit) {
				if (LastChar () == '\\') {
					return TriggerState.DirectorySeparator;
				}

				//explicit trigger grabs back to last slash, if any
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
			var error = triggerNode as ExpressionError;

			if (error == null) {
				ExpressionNode p = triggerNode.Parent;
				while (p != null && error == null) {
					error = p as IncompleteExpressionError;
					p = p.Parent;
				}
			}

			if (triggerNode == error && !(error is IncompleteExpressionError)) {
				triggerNode = error.Parent;
			}

			if (error is ExpressionError ee && ee.WasEOF) {
				ExpressionNode parent = triggerNode.Parent is ExpressionError err ? err.Parent : triggerNode.Parent;
				switch (triggerNode) {
				case ExpressionItem _:
					if (ee.Kind == ExpressionErrorKind.ExpectingMethodOrTransform) {
						return TriggerState.ItemFunctionName;
					}
					break;
				case ExpressionItemName ein:
					if (ee.Kind == ExpressionErrorKind.ExpectingRightParenOrDash) {
						if (ShouldTriggerName (ein.Name)) {
							triggerLength = ein.Name.Length;
							return TriggerState.ItemName;
						}
						return TriggerState.None;
					}
					break;
				case ExpressionPropertyName pn:
					if (ee.Kind == ExpressionErrorKind.ExpectingRightParenOrPeriod) {
						if (ShouldTriggerName (pn.Name)) {
							triggerLength = pn.Name.Length;
							return TriggerState.PropertyName;
						}
						return TriggerState.None;
					}
					break;
				case ExpressionFunctionName fn:
					if (ee.Kind == ExpressionErrorKind.IncompleteProperty) {
						if (ShouldTriggerName (fn.Name)) {
							triggerLength = fn.Name.Length;
							return TriggerState.PropertyFunctionName;
						}
						return TriggerState.None;
					}
					if (ee.Kind == ExpressionErrorKind.ExpectingLeftParen) {
						if (ShouldTriggerName (fn.Name)) {
							triggerLength = fn.Name.Length;
							return TriggerState.ItemFunctionName;
						}
						return TriggerState.None;
					}
					break;
				case ExpressionPropertyFunctionInvocation _:
					if (ee.Kind == ExpressionErrorKind.ExpectingMethodName) {
						return TriggerState.PropertyFunctionName;
					}
					if (ee.Kind == ExpressionErrorKind.ExpectingClassName) {
						return TriggerState.PropertyFunctionClassName;
					}
					break;
				case ExpressionClassReference cr:
					if (ee.Kind == ExpressionErrorKind.ExpectingBracketColonColon
						|| ((ee.Kind == ExpressionErrorKind.ExpectingRightParenOrValue || ee.Kind == ExpressionErrorKind.ExpectingRightParenOrComma) && parent is ExpressionArgumentList)
						) {
						if (ShouldTriggerName (cr.Name)) {
							triggerLength = cr.Name.Length;
							return parent is ExpressionArgumentList? TriggerState.BareFunctionArgumentValue : TriggerState.PropertyFunctionClassName;
						}
						return TriggerState.None;
					}
					break;
				case ExpressionMetadata m:
					if (ee.Kind == ExpressionErrorKind.ExpectingMetadataName) {
						return TriggerState.MetadataName;
					}
					if (ee.Kind == ExpressionErrorKind.ExpectingRightParenOrPeriod) {
						if (ShouldTriggerName (m.ItemName)) {
							triggerLength = m.ItemName.Length;
							return TriggerState.MetadataOrItemName;
						}
						return TriggerState.None;
					}
					if (ee.Kind == ExpressionErrorKind.ExpectingRightParen) {
						if (ShouldTriggerName (m.MetadataName)) {
							triggerLength = m.MetadataName.Length;
							return TriggerState.MetadataName;
						}
						return TriggerState.None;
					}
					break;
				case ExpressionText expressionText: {
						if (
							(error.Kind == ExpressionErrorKind.IncompleteString && (parent is ExpressionArgumentList || parent is ExpressionItemTransform || parent is ExpressionConditionOperator))
							|| (error.Kind == ExpressionErrorKind.ExpectingRightParenOrValue && parent is ExpressionArgumentList)
							) {
							var s = GetTriggerState (
								expressionText.Value, reason, typedChar, false,
								out triggerLength, out triggerExpression, out triggerNode, out _);
							if (error.Kind != ExpressionErrorKind.IncompleteString && s == TriggerState.Value) {
								return TriggerState.BareFunctionArgumentValue;
							}
							return s;
						}
					}
					break;
				case ExpressionArgumentList _: {
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
				case ExpressionErrorKind.ExpectingClassName:
					return TriggerState.PropertyFunctionClassName;
				}
				return TriggerState.None;
			}

			return TriggerState.None;

			char LastChar () => expression[expression.Length - 1];
			char PenultimateChar () => expression[expression.Length - 2];
			bool IsPossiblePathSegmentStart (char c) => c == '_' || char.IsLetterOrDigit (c) || c == '.';
			bool ShouldTriggerName (string n) =>
				isExplicit
				|| (isTypedChar && n.Length == 1)
				|| (isBackspace && n.Length == 0);
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

		static IReadOnlyList<ExpressionNode> GetComparandVariables (ExpressionNode triggerNode)
		{
			while (triggerNode != null) {
				if (triggerNode.Parent is ExpressionConditionOperator op) {
					if (triggerNode == op.Right) {
						return op.Left.WithAllDescendants ().OfType<ExpressionProperty> ().ToList ();
					}
					break;
				}
				triggerNode = triggerNode.Parent;
			}
			return Array.Empty<ExpressionNode> ();
		}

		public static IEnumerable<BaseInfo> GetComparandCompletions (MSBuildRootDocument doc, IReadOnlyList<ExpressionNode> variables)
		{
			var names = new HashSet<string> (); yield break;/*
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
			}*/
		}

		public static IEnumerable<BaseInfo> GetCompletionInfos (
			MSBuildResolveResult rr,
			TriggerState trigger, MSBuildValueKind kind,
			ExpressionNode triggerExpression, int triggerLength,
			MSBuildRootDocument doc, IFunctionTypeProvider functionTypeProvider)
		{
			switch (trigger) {
			case TriggerState.Value:
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
