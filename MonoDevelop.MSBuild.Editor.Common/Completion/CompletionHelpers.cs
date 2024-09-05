// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Language.Syntax;
using MonoDevelop.MSBuild.Language.Typesystem;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Editor.Completion;

// code extracted from MSBuildCompletionSource for use by LanguageServer
internal class CompletionHelpers
{
	//FIXME: improve logic for determining where metadata is permitted
	public static bool IsMetadataAllowed (ExpressionNode triggerExpression, MSBuildResolveResult rr)
	{
		//if any a parent node is an item transform or function, metadata is allowed
		if (triggerExpression != null) {
			var node = triggerExpression.Find (triggerExpression.Length);
			while (node != null) {
				if (node is ExpressionItemTransform || node is ExpressionItemFunctionInvocation) {
					return true;
				}
				node = node.Parent;
			}
		}

		if (rr.AttributeSyntax != null) {
			switch (rr.AttributeSyntax.SyntaxKind) {
			// metadata attributes on items can refer to other metadata on the items
			case MSBuildSyntaxKind.Item_Metadata:
			// task params can refer to metadata in batched items
			case MSBuildSyntaxKind.Task_Parameter:
			// target inputs and outputs can use metadata from each other's items
			case MSBuildSyntaxKind.Target_Inputs:
			case MSBuildSyntaxKind.Target_Outputs:
				return true;
			//conditions on metadata elements can refer to metadata on the items
			case MSBuildSyntaxKind.Metadata_Condition:
				return true;
			}
		}

		if (rr.ElementSyntax != null) {
			switch (rr.ElementSyntax.SyntaxKind) {
			// metadata elements can refer to other metadata in the items
			case MSBuildSyntaxKind.Metadata:
				return true;
			}
		}
		return false;
	}

	public static bool ShouldAddHintForCompletions (ITypedSymbol symbol)
		=> symbol.ValueKindWithoutModifiers () switch {
			MSBuildValueKind.WarningCode => true,
			MSBuildValueKind.CustomType when symbol.CustomType is CustomTypeInfo ct => ct.BaseKind switch {
				MSBuildValueKind.Guid => true,
				MSBuildValueKind.Int => true,
				MSBuildValueKind.WarningCode => true,
				_ => false
			},
			_ => false
		};

	public static bool TryGetElementSyntaxForElementCompletion (List<XObject> nodePath, out MSBuildElementSyntax? languageElement, out string? elementName)
	{
		// we can't use the LanguageElement from the resolveresult for element completion.
		// if completion is triggered in an existing element's name, the resolveresult
		// will be for that element, so completion will be for the element's children
		// rather than for the element itself.
		languageElement = null;
		elementName = null;
		for (int i = 1; i < nodePath.Count; i++) {
			if (nodePath[i] is XElement el) {
				elementName = el.Name.Name;
				languageElement = MSBuildElementSyntax.Get (elementName, languageElement);
				continue;
			}
			return false;
		}

		// if we don't have a language element and we're not at root level, we're in an invalid location
		if (languageElement == null && nodePath.Count > 2) {
			return false;
		}

		return true;
	}
}