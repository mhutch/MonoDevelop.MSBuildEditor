// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.MSBuild.Analysis;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Schema;

namespace MonoDevelop.MSBuild.Language;

class ExpressionDiagnostics
{
	public static (MSBuildDiagnosticDescriptor, object[]) GetExpressionError (ExpressionError error, ITypedSymbol info)
	{
		(MSBuildDiagnosticDescriptor, object[]) Return (MSBuildDiagnosticDescriptor desc, params object[] args) => (desc, args);
		return error.Kind switch {
			ExpressionErrorKind.MetadataDisallowed => Return (MetadataDisallowed, DescriptionFormatter.GetKindNoun (info), info.Name),
			ExpressionErrorKind.EmptyListEntry => Return (EmptyListValue),
			ExpressionErrorKind.ExpectingItemName => Return (ExpectingItemName),
			ExpressionErrorKind.ExpectingRightParen => Return (ExpectingChar, ')'),
			ExpressionErrorKind.ExpectingRightParenOrPeriod => Return (ExpectingCharOrChar, ')', '.'),
			ExpressionErrorKind.ExpectingPropertyName => Return (ExpectingPropertyName),
			ExpressionErrorKind.ExpectingMetadataName => Return (ExpectingMetadataName),
			ExpressionErrorKind.ExpectingMetadataOrItemName => Return (ExpectingMetadataOrItemName),
			ExpressionErrorKind.ExpectingRightAngleBracket => Return (ExpectingChar, '>'),
			ExpressionErrorKind.ExpectingRightParenOrDash => Return (ExpectingCharOrChar, ')', '-'),
			ExpressionErrorKind.ItemsDisallowed => Return (ItemsDisallowed, DescriptionFormatter.GetKindNoun (info), info.Name),
			ExpressionErrorKind.ExpectingMethodOrTransform => Return (ExpectingFunctionOrTransform),
			ExpressionErrorKind.ExpectingMethodName => Return (ExpectingFunctionName),
			ExpressionErrorKind.ExpectingLeftParen => Return (ExpectingChar, '('),
			ExpressionErrorKind.ExpectingRightParenOrComma => Return (ExpectingCharOrChar, ')', ','),
			ExpressionErrorKind.ExpectingRightParenOrValue => Return (ExpectingRightParenOrValue),
			ExpressionErrorKind.ExpectingValue => Return (ExpectingValue),
			ExpressionErrorKind.CouldNotParseNumber => Return (CouldNotParseNumber),
			ExpressionErrorKind.IncompleteValue => Return (IncompleteValue),
			ExpressionErrorKind.ExpectingBracketColonColon => Return (ExpectingChar, "]::"),
			ExpressionErrorKind.ExpectingClassName => Return (ExpectingClassName),
			ExpressionErrorKind.ExpectingClassNameComponent => Return (IncompleteClassName),
			ExpressionErrorKind.IncompleteString => Return (IncompleteString),
			ExpressionErrorKind.IncompleteProperty => Return (IncompleteProperty),
			ExpressionErrorKind.IncompleteOperator => Return (IncompleteOperator),
			ExpressionErrorKind.ExpectingEquals => Return (ExpectingChar, '='),
			ExpressionErrorKind.IncompleteOrUnsupportedEntity => Return (IncompleteOrUnsupportedEntity),
			ExpressionErrorKind.UnexpectedCharacter => throw new System.NotImplementedException (),
			_ => throw new System.Exception ($"Unhandled ExpressionErrorKind '{error.Kind}'")
		};
	}

	public const string MetadataDisallowed_Id = nameof(MetadataDisallowed);
	public static readonly MSBuildDiagnosticDescriptor MetadataDisallowed = new (
		MetadataDisallowed_Id,
		"Metadata not permitted",
		"{0} `{1}` does not permit metadata",
		MSBuildDiagnosticSeverity.Error);

	public const string ItemsDisallowed_Id = nameof(ItemsDisallowed);
	public static readonly MSBuildDiagnosticDescriptor ItemsDisallowed = new (
		ItemsDisallowed_Id,
		"Items not permitted",
		"{0} `{1}` does not permit items",
		MSBuildDiagnosticSeverity.Error);

	public const string EmptyListValue_Id = nameof(EmptyListValue);
	public static readonly MSBuildDiagnosticDescriptor EmptyListValue = new (
		EmptyListValue_Id,
		"Empty list value",
		MSBuildDiagnosticSeverity.Warning);

	public const string ExpectingItemName_Id = nameof(ExpectingItemName);
	public static readonly MSBuildDiagnosticDescriptor ExpectingItemName = new (
		ExpectingItemName_Id,
		"Expecting item name",
		MSBuildDiagnosticSeverity.Error);

	public const string ExpectingChar_Id = nameof(ExpectingChar);
	public static readonly MSBuildDiagnosticDescriptor ExpectingChar = new (
		ExpectingChar_Id,
		"Expecting `{0}`",
		MSBuildDiagnosticSeverity.Error);

	public const string ExpectingCharOrChar_Id = nameof(ExpectingCharOrChar);
	public static readonly MSBuildDiagnosticDescriptor ExpectingCharOrChar = new (
		ExpectingCharOrChar_Id,
		"Expecting `{0}` or `{1}`",
		MSBuildDiagnosticSeverity.Error);

	public static readonly MSBuildDiagnosticDescriptor ExpectingPropertyName= new (
		nameof (ExpectingPropertyName),
		"Expecting property name",
		MSBuildDiagnosticSeverity.Error);

	public const string ExpectingMetadataName_Id = nameof(ExpectingMetadataName);
	public static readonly MSBuildDiagnosticDescriptor ExpectingMetadataName = new (
		ExpectingMetadataName_Id,
		"Expecting metadata name",
		MSBuildDiagnosticSeverity.Error);

	public const string ExpectingMetadataOrItemName_Id = nameof(ExpectingMetadataOrItemName);
	public static readonly MSBuildDiagnosticDescriptor ExpectingMetadataOrItemName = new (
		ExpectingMetadataOrItemName_Id,
		"Expecting metadata or item name",
		MSBuildDiagnosticSeverity.Error);

	public const string ExpectingFunctionName_Id = nameof(ExpectingFunctionName);
	public static readonly MSBuildDiagnosticDescriptor ExpectingFunctionName = new (
		ExpectingFunctionName_Id,
		"Expecting function name",
		MSBuildDiagnosticSeverity.Error);

	public const string ExpectingValue_Id = nameof(ExpectingValue);
	public static readonly MSBuildDiagnosticDescriptor ExpectingValue = new (
		ExpectingValue_Id,
		"Expecting value",
		MSBuildDiagnosticSeverity.Error);

	public const string ExpectingFunctionOrTransform_Id = nameof(ExpectingFunctionOrTransform);
	public static readonly MSBuildDiagnosticDescriptor ExpectingFunctionOrTransform = new (
		ExpectingFunctionOrTransform_Id,
		"Expecting item function or transform",
		MSBuildDiagnosticSeverity.Error);

	public const string ExpectingClassName_Id = nameof(ExpectingClassName);
	public static readonly MSBuildDiagnosticDescriptor ExpectingClassName = new (
		ExpectingClassName_Id,
		"Expecting class name",
		MSBuildDiagnosticSeverity.Error);

	public const string IncompleteClassName_Id = nameof(IncompleteClassName);
	public static readonly MSBuildDiagnosticDescriptor IncompleteClassName = new (
		IncompleteClassName_Id,
		"Incomplete class name",
		MSBuildDiagnosticSeverity.Error);

	public const string IncompleteString_Id = nameof(IncompleteString);
	public static readonly MSBuildDiagnosticDescriptor IncompleteString = new (
		IncompleteString_Id,
		"Incomplete string",
		MSBuildDiagnosticSeverity.Error);

	public const string IncompleteValue_Id = nameof(IncompleteValue);
	public static readonly MSBuildDiagnosticDescriptor IncompleteValue = new (
		IncompleteValue_Id,
		"Incomplete value",
		MSBuildDiagnosticSeverity.Error);

	public const string IncompleteProperty_Id = nameof(IncompleteProperty);
	public static readonly MSBuildDiagnosticDescriptor IncompleteProperty = new (
		IncompleteProperty_Id,
		"Incomplete property",
		MSBuildDiagnosticSeverity.Error);

	public const string IncompleteOperator_Id = nameof (IncompleteOperator);
	public static readonly MSBuildDiagnosticDescriptor IncompleteOperator = new (
		IncompleteOperator_Id,
		"Incomplete operator",
		MSBuildDiagnosticSeverity.Error);

	public const string IncompleteOrUnsupportedEntity_Id = nameof (IncompleteOrUnsupportedEntity);
	public static readonly MSBuildDiagnosticDescriptor IncompleteOrUnsupportedEntity = new (
		IncompleteOrUnsupportedEntity_Id,
		"Incomplete or unsupported property",
		MSBuildDiagnosticSeverity.Error);

	public const string UnexpectedCharacter_Id = nameof (UnexpectedCharacter);
	public static readonly MSBuildDiagnosticDescriptor UnexpectedCharacter = new (
		UnexpectedCharacter_Id,
		"Incomplete or unsupported property",
		MSBuildDiagnosticSeverity.Error);

	public const string CouldNotParseNumber_Id = nameof(CouldNotParseNumber);
	public static readonly MSBuildDiagnosticDescriptor CouldNotParseNumber = new (
		CouldNotParseNumber_Id,
		"Invalid number format",
		MSBuildDiagnosticSeverity.Error);

	public const string ExpectingRightParenOrValue_Id = nameof(ExpectingRightParenOrValue);
	public static readonly MSBuildDiagnosticDescriptor ExpectingRightParenOrValue = new (
		ExpectingRightParenOrValue_Id,
		"Expecting `)` or value",
		MSBuildDiagnosticSeverity.Error);
}
