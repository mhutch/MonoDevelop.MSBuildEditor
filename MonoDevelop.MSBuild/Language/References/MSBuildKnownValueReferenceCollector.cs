// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP
#nullable enable
#endif

using System;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Language.Syntax;
using MonoDevelop.MSBuild.Language.Typesystem;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Parser;

namespace MonoDevelop.MSBuild.Language.References;

class MSBuildKnownValueReferenceCollector : MSBuildReferenceCollector
{
	readonly MSBuildValueKind kind;
	readonly HashSet<string> knownValues;
	readonly CustomTypeInfo? customType;
	readonly bool isWarningCode;

	public MSBuildKnownValueReferenceCollector (MSBuildDocument document, ITextSource textSource, ILogger logger, ITypedSymbol knownValue, FindReferencesReporter reportResult)
		: base (document, textSource, logger, knownValue.Name, reportResult)
	{
		kind = knownValue.ValueKind;

		// NuGet completion stuffs some other stuff in customType.
		// Check kind in advance so we don't have to check that elsewhere in this class.
		customType = kind.WithoutModifiers () == MSBuildValueKind.CustomType ? knownValue.CustomType : null;

		// warning codes are special, derived warning code values are valid in symbols of the base warning code type
		isWarningCode = customType is not null && customType.BaseKind == MSBuildValueKind.WarningCode;

		bool isCaseSensitive = customType?.CaseSensitive ?? false;
		knownValues = new HashSet<string> (isCaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase) {
			knownValue.Name
		};

		if (knownValue is CustomTypeValue ctv && ctv.Aliases is not null) {
			foreach (var alias in ctv.Aliases) {
				knownValues.Add (alias);
			}
		}
	}

	protected override bool IsMatch (string? name) => name is not null && knownValues.Contains (name);

	protected override void VisitValue (
		XElement element, XAttribute? attribute,
		MSBuildElementSyntax elementSyntax, MSBuildAttributeSyntax? attributeSyntax,
		ITypedSymbol elementSymbol, ITypedSymbol? attributeSymbol,
		string expressionText, ExpressionNode node)
	{
		var valueSymbol = attributeSymbol ?? elementSymbol;

		if (!IsTypeMatch ()) {
			return;
		}

		switch (node) {
		case ListExpression list:
			if (!valueSymbol.AllowsLists ()) {
				return;
			}
			foreach (var c in list.Nodes) {
				if (c is ExpressionText l) {
					CheckValueMatch (l);
				}
			}
			break;
		case ExpressionText lit:
			CheckValueMatch (lit);
			break;
		}

		bool IsTypeMatch()
		{
			var valueKind = valueSymbol.ValueKindWithoutModifiers ();
			if (customType is not null) {
				// if the value is a warning code, it's valid for symbols of the base warning code type
				if (isWarningCode && valueKind == MSBuildValueKind.WarningCode) {
					return true;
				}
				// exact custom type match
				if (valueSymbol.CustomType == customType) {
					return true;
				}
				// in case types are defined in multiple schemas, consider named types with the same name to be the same type
				if (customType.Name is not null && valueSymbol.CustomType?.Name == customType.Name) {
					return true;
				}
				return false;
			}
			return valueKind == kind;
		}

		void CheckValueMatch (ExpressionText node)
		{
			// TODO: valueSymbol should support specifying whether to ignore whitespace or not
			if (IsPureMatch (node, out int offset, out int length, ignoreWhitespace: true)) {
				// TODO: this isn't really accurate, maybe we need a new type for constant references?
				AddResult (offset, length, ReferenceUsage.Read);
			}
		}
	}

}