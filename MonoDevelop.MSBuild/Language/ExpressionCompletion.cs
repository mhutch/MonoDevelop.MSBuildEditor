// Copyright (c) 2014 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Extensions.Logging;

using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.MSBuild.Language.Typesystem;
using MonoDevelop.Xml.Parser;

namespace MonoDevelop.MSBuild.Language
{
	static partial class ExpressionCompletion
	{
		public static bool IsPossibleExpressionCompletionContext (XmlSpineParser parser)
			=> parser.IsInAttributeValue () || parser.IsInText () || parser.IsRootFree ();

		//validates CommaValue and SemicolonValue, and collapses them to Value
		public static bool ValidateListPermitted (ListKind listKind, MSBuildValueKind kind)
			=> listKind switch {
				ListKind.Comma => kind.AllowsLists (MSBuildValueKind.ListComma),
				ListKind.Semicolon => kind.AllowsLists (MSBuildValueKind.ListSemicolon),
				ListKind.None => true,
				_ => throw new ArgumentException($"Unknown ListKind '{listKind}'", nameof(listKind))
			};

		public static bool IsCondition (this MSBuildResolveResult rr)
		{
			return rr.AttributeSyntax != null && rr.AttributeSyntax.ValueKind == MSBuildValueKind.Condition;
		}

		public static TriggerState GetTriggerState (
			string expression, int triggerPos, TriggerReason reason, char typedChar, bool isCondition,
			out int spanStart, out int spanLength, out ExpressionNode triggerExpression,
			out ListKind listKind, out IReadOnlyList<ExpressionNode> comparandVariables, ILogger logger)
		{
			comparandVariables = null;

			var state = GetTriggerState (expression, triggerPos, reason, typedChar, isCondition, out spanStart, out spanLength, out triggerExpression, out var triggerNode, out listKind, logger);

			if (state != TriggerState.None && isCondition) {
				comparandVariables = GetComparandVariables (triggerNode).ToList ();
			}

			return state;
		}

		static TriggerState GetTriggerState (
			string expression, int triggerPos, TriggerReason reason, char typedChar, bool isCondition,
			out int spanStart, out int spanLength, out ExpressionNode triggerExpression, out ExpressionNode triggerNode,
			out ListKind listKind, ILogger logger)
		{
			spanStart = triggerPos;
			spanLength = 0;
			listKind = ListKind.None;

			var isNewline = typedChar == '\n';
			var isTypedChar = reason == TriggerReason.TypedChar;

			if (isTypedChar && !isNewline && expression.Length > 0 && expression[expression.Length - 1] != typedChar) {
				triggerExpression = null;
				triggerNode = null;
				LogInconsistentExpressionAndTypedChar (logger, expression, typedChar);
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

			return GetTriggerState (triggerExpression, triggerPos, reason, typedChar, out spanStart, out spanLength, out triggerNode, out listKind, logger);
		}

		static TriggerState GetTriggerState (
			ExpressionNode triggerExpression, int triggerPos, TriggerReason reason, char typedChar,
			out int spanStart, out int spanLength, out ExpressionNode triggerNode, out ListKind listKind, ILogger logger)
		{
			spanStart = triggerPos;
			spanLength = 0;

			listKind = ListKind.None;

			var isExplicit = reason == TriggerReason.Invocation;
			var isBackspace = reason == TriggerReason.Backspace;
			var isTypedChar = reason == TriggerReason.TypedChar;

			if (triggerExpression is ListExpression el) {
				//the last list entry is the thing that triggered it
				triggerExpression = el.Nodes.Last ();
				listKind = el.Separator == ',' ? ListKind.Comma : ListKind.Semicolon;
				if (triggerExpression is ExpressionError e && e.Kind == ExpressionErrorKind.EmptyListEntry) {
					spanLength = 0;
					triggerNode = triggerExpression;
					return TriggerState.Value;
				}
			}

			if (triggerExpression is ExpressionText text) {
				//automatically trigger at the start of an expression regardless
				if (text.Length == 0) {
					triggerNode = triggerExpression;
					return TriggerState.Value;
				}

				if (typedChar == '\\' || (!isTypedChar && text.Value[text.Length-1] == '\\')) {
					spanLength = 0;
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
					spanLength = 0;
					triggerNode = triggerExpression;
					return isExplicit ? TriggerState.Value : TriggerState.None;
				}

				//auto trigger on first char
				if (length == 1 && !isBackspace) {
					spanLength = 1;
					spanStart = triggerPos - spanLength;
					triggerNode = triggerExpression;
					return TriggerState.Value;
				}

				if (isExplicit) {
					var lastSlash = text.Value.LastIndexOf ('\\');
					if (lastSlash != -1) {
						spanLength = text.Length - lastSlash - 1;
					triggerNode = triggerExpression;
						return TriggerState.DirectorySeparator;
					}
					spanLength = length;
					spanStart = triggerPos - spanLength;
					triggerNode = triggerExpression;
					return TriggerState.Value;
				}

				triggerNode = null;
				return TriggerState.None;
			}

			//find the deepest node that touches the end
			triggerNode = triggerExpression.Find (triggerExpression.End);
			if (triggerNode == null) {
				return TriggerState.None;
			}

			// if inside a quoted expression, scope down
			if (triggerNode != triggerExpression) {
				ExpressionNode p = triggerNode.Parent;
				while (p != null && p != triggerExpression) {
					if (p is QuotedExpression quotedExpr) {
						return GetTriggerState (
								quotedExpr.Expression, triggerPos, reason, typedChar,
								out spanStart, out spanLength, out triggerNode, out _, logger);
					}
					p = p.Parent;
				}
			}

			// path separator completion
			if (triggerNode is ExpressionText lit) {
				if (lit.Length > 0 && lit.Value[lit.Length - 1] == '\\') {
					return TriggerState.DirectorySeparator;
				}

				//explicit trigger grabs back to last slash, if any
				if (isExplicit) {
					var lastSlash = lit.Value.LastIndexOf ('\\');
					if (lastSlash != -1) {
						spanLength = lit.Length - lastSlash - 1;
						spanStart = triggerPos - spanLength;
						return TriggerState.DirectorySeparator;
					}
				}

				//eager trigger on first char after /
				if (!isExplicit && lit.Length > 1 && lit.Value[lit.Value.Length-2] == '\\' && IsPossiblePathSegmentStart (typedChar)) {
					spanLength = 1;
					spanStart = triggerPos - spanLength;
					return TriggerState.DirectorySeparator;
				}
			}

			//find the deepest error
			var error = triggerNode as ExpressionError;
			if (error == null) {
				ExpressionNode p = triggerNode.Parent;
				while (p != null && error == null) {
					error = p as IncompleteExpressionError;
					if (p == triggerExpression) {
						break;
					}
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
							spanLength = ein.Name.Length;
							spanStart = triggerPos - spanLength;
							return TriggerState.ItemName;
						}
						return TriggerState.None;
					}
					break;
				case ExpressionPropertyName pn:
					if (ee.Kind == ExpressionErrorKind.ExpectingRightParenOrPeriod) {
						if (ShouldTriggerName (pn.Name)) {
							spanLength = pn.Name.Length;
							spanStart = triggerPos - spanLength;
							return TriggerState.PropertyName;
						}
						return TriggerState.None;
					}
					break;
				case ExpressionFunctionName fn:
					if (ee.Kind == ExpressionErrorKind.IncompleteProperty) {
						if (ShouldTriggerName (fn.Name)) {
							spanLength = fn.Name.Length;
							spanStart = triggerPos - spanLength;
							if (IsConditionFunctionError (ee)) {
								return TriggerState.ConditionFunctionName;
							}
							return TriggerState.PropertyFunctionName;
						}
						return TriggerState.None;
					}
					if (ee.Kind == ExpressionErrorKind.ExpectingLeftParen) {
						if (ShouldTriggerName (fn.Name)) {
							spanLength = fn.Name.Length;
							spanStart = triggerPos - spanLength;
							if (IsConditionFunctionError (ee)) {
								return TriggerState.ConditionFunctionName;
							}
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
							spanLength = cr.Name.Length;
							spanStart = triggerPos - spanLength;
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
							spanLength = m.ItemName.Length;
							spanStart = triggerPos - spanLength;
							return TriggerState.MetadataOrItemName;
						}
						return TriggerState.None;
					}
					if (ee.Kind == ExpressionErrorKind.ExpectingRightParen) {
						if (ShouldTriggerName (m.MetadataName)) {
							spanLength = m.MetadataName.Length;
							spanStart = triggerPos - spanLength;
							return TriggerState.MetadataName;
						}
						return TriggerState.None;
					}
					break;
				case ExpressionText expressionText: {
						if (
							(error.Kind == ExpressionErrorKind.IncompleteString
								&& (parent is ExpressionArgumentList || parent is ExpressionItemTransform || parent is ExpressionConditionOperator || parent is QuotedExpression))
							|| (error.Kind == ExpressionErrorKind.ExpectingRightParenOrValue && parent is ExpressionArgumentList)
							) {
							var s = GetTriggerState (
								expressionText.Value, triggerPos, reason, typedChar, false,
								out spanStart, out spanLength, out triggerExpression, out triggerNode, out _, logger);
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

			bool IsPossiblePathSegmentStart (char c) => c == '_' || char.IsLetterOrDigit (c) || c == '.';
			bool ShouldTriggerName (string n) =>
				isExplicit
				|| (isTypedChar && n.Length == 1)
				|| (isBackspace && n.Length == 0);

			bool IsConditionFunctionError (ExpressionError ee)
				=> ee.Parent is ExpressionConditionOperator
					|| ee is IncompleteExpressionError iee && iee.IncompleteNode is ExpressionConditionFunction;
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
			ConditionFunctionName,
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

		static IEnumerable<ExpressionNode> GetComparandVariables (ExpressionNode triggerNode)
		{
			while (triggerNode != null) {
				if (triggerNode.Parent is ExpressionConditionOperator op) {
					if (triggerNode == op.Right) {
						foreach (var node in op.Left.WithAllDescendants ()) {
							switch (node) {
							case ExpressionProperty p: yield return p; break;
							case ExpressionMetadata m: yield return m; break;
							}
						}
					}
					break;
				}
				triggerNode = triggerNode.Parent;
			}
		}

		public static IEnumerable<ISymbol> GetComparandCompletions (MSBuildRootDocument doc, IMSBuildFileSystem fileSystem, IReadOnlyList<ExpressionNode> variables, ILogger logger)
		{
			var names = new HashSet<string> ();
			foreach (var variable in variables) {
				VariableInfo info;
				switch (variable) {
				case ExpressionProperty ep:
					if (ep.IsSimpleProperty) {
						info = doc.GetProperty (ep.Name, true) ?? new PropertyInfo (ep.Name, null);
						break;
					}
					continue;
				case ExpressionMetadata em:
					info = doc.GetMetadata (em.ItemName, em.MetadataName, true) ?? new MetadataInfo (em.MetadataName, null);
					break;
				default:
					continue;
				}

				if (info == null) {
					continue;
				}

				IEnumerable<ISymbol> cinfos;
				if (info.CustomType != null && info.CustomType.Values.Count > 0) {
					cinfos = info.CustomType.Values;
				} else {
					var kind = info.InferValueKindIfUnknown ();
					cinfos = MSBuildCompletionExtensions.GetValueCompletions (kind, doc, fileSystem, logger);
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

		public static IEnumerable<ISymbol> GetCompletionInfos (
			MSBuildResolveResult rr,
			TriggerState trigger, MSBuildValueKind kind,
			ExpressionNode triggerExpression, int triggerLength,
			MSBuildRootDocument doc, IFunctionTypeProvider functionTypeProvider,
			IMSBuildFileSystem fileSystem,
			ILogger logger)
		{
			switch (trigger) {
			case TriggerState.Value:
				return MSBuildCompletionExtensions.GetValueCompletions (kind, doc, fileSystem, logger, rr, triggerExpression);
			case TriggerState.ItemName:
				return doc.GetItems ();
			case TriggerState.MetadataName:
				return doc.GetMetadata (null, true);
			case TriggerState.PropertyName:
				bool includeReadOnly = rr.AttributeSyntax?.SyntaxKind != Syntax.MSBuildSyntaxKind.Output_PropertyName;
				return doc.GetProperties (includeReadOnly);
			case TriggerState.MetadataOrItemName:
				return ((IEnumerable<ISymbol>)doc.GetItems ()).Concat (doc.GetMetadata (null, true));
			case TriggerState.DirectorySeparator:
				return fileSystem.GetFilenameCompletions (kind, doc, triggerExpression, triggerLength, logger, rr);
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
			case TriggerState.ConditionFunctionName:
				return MSBuildIntrinsics.ConditionFunctions.Values;
			}
			throw new InvalidOperationException ($"Unhandled TriggerState {trigger}");
		}

		[LoggerMessage (EventId = 0, Level = LogLevel.Warning, Message = "Expression text '{expression}' is not consistent with typed character '{typedChar}")]
		static partial void LogInconsistentExpressionAndTypedChar (ILogger logger, string expression, char typedChar);
	}
}
