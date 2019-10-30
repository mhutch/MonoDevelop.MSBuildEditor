// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using MonoDevelop.MSBuild.Analysis;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Schema;

namespace MonoDevelop.MSBuild.Language
{
	class CoreDiagnostics
	{
		public static MSBuildDiagnosticDescriptor EmptySdkAttribute = new MSBuildDiagnosticDescriptor (
			"EmptySdkAttribute",
			"Empty SDK attribute",
			null,
			MSBuildDiagnosticSeverity.Error
		);

		public static MSBuildDiagnosticDescriptor InvalidSdkAttribute = new MSBuildDiagnosticDescriptor (
			"InvalidSdkAttribute",
			"Invalid SDK attribute",
			"The SDK attribute '{0}' has an invalid format",
			MSBuildDiagnosticSeverity.Error
		);

		public static MSBuildDiagnosticDescriptor SdkNotFound = new MSBuildDiagnosticDescriptor (
			"SdkNotFound",
			"SDK not found",
			"The SDK '{0}' was not found",
			MSBuildDiagnosticSeverity.Error
		);

		public static MSBuildDiagnosticDescriptor InternalError = new MSBuildDiagnosticDescriptor (
			"InternalError",
			"Internal error",
			"An internal error occurred: {0}",
			MSBuildDiagnosticSeverity.Error
		);

		public static MSBuildDiagnosticDescriptor UnresolvedImport = new MSBuildDiagnosticDescriptor (
			"UnresolvedImport",
			"Could not resolve import",
			"The import '{0}' could not be resolved",
			MSBuildDiagnosticSeverity.Warning
		);

		public static MSBuildDiagnosticDescriptor UnknownElement = new MSBuildDiagnosticDescriptor (
			"UnknownElement",
			"Unknown element",
			"The element '{0}' is not valid at this location",
			MSBuildDiagnosticSeverity.Error
		);

		public static MSBuildDiagnosticDescriptor UnknownAttribute = new MSBuildDiagnosticDescriptor (
			"UnknownAttribute",
			"Unknown attribute",
			"The attribute '{0}' is not valid at this location",
			MSBuildDiagnosticSeverity.Error
		);

		public static MSBuildDiagnosticDescriptor MissingRequiredAttribute = new MSBuildDiagnosticDescriptor (
			"MissingRequiredAttribute",
			"Missing required attribute",
			"The element '{0}' is missing the required attribute '{1}'",
			MSBuildDiagnosticSeverity.Error
		);

		public static MSBuildDiagnosticDescriptor UnexpectedText = new MSBuildDiagnosticDescriptor (
			"UnexpectedText",
			"Unexpected text content",
			"The element '{0}' does not permit text content",
			MSBuildDiagnosticSeverity.Error
		);

		public static MSBuildDiagnosticDescriptor RequiredAttributeEmpty = new MSBuildDiagnosticDescriptor (
			"RequiredAttributeEmpty",
			"Empty required attribute",
			"The required attribute '{0}' has an empty value",
			MSBuildDiagnosticSeverity.Error
		);

		public static MSBuildDiagnosticDescriptor AttributeEmpty = new MSBuildDiagnosticDescriptor (
			"AttributeEmpty",
			"Empty attribute",
			"The attribute '{0}' has an empty value",
			MSBuildDiagnosticSeverity.Warning
		);

		public static MSBuildDiagnosticDescriptor Deprecated = new MSBuildDiagnosticDescriptor (
			"Deprecated",
			"Deprecated {0}",
			"The {0} '{1}' is deprecated",
			MSBuildDiagnosticSeverity.Warning
		);

		public static MSBuildDiagnosticDescriptor DeprecatedWithMessage = new MSBuildDiagnosticDescriptor (
			"DeprecatedWithMessage",
			"Deprecated {0}",
			"The {0} '{1}' is deprecated: {2}",
			MSBuildDiagnosticSeverity.Warning
		);

		public static MSBuildDiagnosticDescriptor TaskNotDefined = new MSBuildDiagnosticDescriptor (
			"TaskNotDefined",
			"Task not defined",
			"The task '{0}' is not defined",
			MSBuildDiagnosticSeverity.Error
		);

		public static MSBuildDiagnosticDescriptor UnknownTaskParameter = new MSBuildDiagnosticDescriptor (
			"UnknownTaskParameter",
			"Unknown task parameter",
			"Unknown parameter '{1}' on task '{1}'",
			MSBuildDiagnosticSeverity.Error
		);

		public static MSBuildDiagnosticDescriptor EmptyRequiredTaskParameter = new MSBuildDiagnosticDescriptor (
			"EmptyRequiredTaskParameter",
			"Empty required task parameter",
			"Required parameter '{1}' on task '{1}' is empty",
			MSBuildDiagnosticSeverity.Error
		);

		public static MSBuildDiagnosticDescriptor MissingRequiredTaskParameter = new MSBuildDiagnosticDescriptor (
			"MissingRequiredTaskParameter",
			"Missing task parameter",
			"Task '{0}' is missing required parameter '{1}'",
			MSBuildDiagnosticSeverity.Error
		);

		public static MSBuildDiagnosticDescriptor NonOutputTaskParameter = new MSBuildDiagnosticDescriptor (
			"NonOutputTaskParameter",
			"Incorrect parameter usage",
			"Parameter '{1}' on task '{0}' cannot be used an an output parameter",
			MSBuildDiagnosticSeverity.Error
		);

		public static MSBuildDiagnosticDescriptor NoTargets = new MSBuildDiagnosticDescriptor (
			"NoTargets",
			"No targets in project",
			"Project does not define or import any targets",
			MSBuildDiagnosticSeverity.Error
		);

		public static MSBuildDiagnosticDescriptor OtherwiseMustBeLastInChoose = new MSBuildDiagnosticDescriptor (
			"OtherwiseMustBeLastInChoose",
			"Otherwise must be last choice",
			"'Otherwise' must be the last choice in a 'Choose'",
			MSBuildDiagnosticSeverity.Error
		);

		public static MSBuildDiagnosticDescriptor OnErrorMustBeLastInTarget = new MSBuildDiagnosticDescriptor (
			"OnErrorMustBeLastInTarget",
			"OnError must be last element in target",
			"In a target, OnError may only be followed by other OnError elements",
			MSBuildDiagnosticSeverity.Error
		);

		public static MSBuildDiagnosticDescriptor OutputMustHavePropertyOrItemName = new MSBuildDiagnosticDescriptor (
			"OutputMustHavePropertyOrItemName",
			"Output must have PropertyName or TaskName",
			"Task Output element must specify a PropertyName or a TaskName",
			MSBuildDiagnosticSeverity.Error
		);

		public static MSBuildDiagnosticDescriptor UsingTaskMustHaveAssembly = new MSBuildDiagnosticDescriptor (
			"UsingTaskMustHaveAssembly",
			"UsingTask requires assembly",
			"UsingTask must have AssemblyName or AssemblyFile attribute",
			MSBuildDiagnosticSeverity.Error
		);

		public static MSBuildDiagnosticDescriptor TaskFactoryCannotHaveAssemblyName = new MSBuildDiagnosticDescriptor (
			"TaskFactoryCannotHaveAssemblyName",
			"TaskFactory cannot have AssemblyName",
			"TaskFactory is not compatible with AssemblyName attribute on UsingTask. Use AssemblyFile instead.",
			MSBuildDiagnosticSeverity.Error
		);

		public static MSBuildDiagnosticDescriptor TaskFactoryMustHaveAssemblyFile = new MSBuildDiagnosticDescriptor (
			"TaskFactoryMustHaveAssemblyFile",
			"TaskFactory must have AssemblyFile",
			"TaskFactory requires AssemblyFile attribute on UsingTask",
			MSBuildDiagnosticSeverity.Error
		);

		public static MSBuildDiagnosticDescriptor TaskFactoryMustHaveOneAssemblyOnly = new MSBuildDiagnosticDescriptor (
			"TaskFactoryMustHaveOneAssemblyOnly",
			"UsingTask can only have one assembly",
			"UsingTask may not have both AssemblyName and AssemblyFile attributes",
			MSBuildDiagnosticSeverity.Error
		);

		public static MSBuildDiagnosticDescriptor OneParameterGroup = new MSBuildDiagnosticDescriptor (
			"OneParameterGroup",
			"One ParameterGroup per UsingTask",
			"Each UsingTask may only have a single ParameterGroup",
			MSBuildDiagnosticSeverity.Error
		);

		public static MSBuildDiagnosticDescriptor OneTaskBody = new MSBuildDiagnosticDescriptor (
			"OneTaskBody",
			"One Task body per UsingTask",
			"Each UsingTask may only have one Task element",
			MSBuildDiagnosticSeverity.Error
		);

		public static MSBuildDiagnosticDescriptor TaskBodyMustHaveFactory = new MSBuildDiagnosticDescriptor (
			"TaskBodyMustHaveFactory",
			"Task body must have factory",
			"UsingTask without TaskFactory attribute cannot have Task element",
			MSBuildDiagnosticSeverity.Error
		);

		public static MSBuildDiagnosticDescriptor ParameterGroupMustHaveFactory = new MSBuildDiagnosticDescriptor (
			"ParameterGroupMustHaveFactory",
			"ParameterGroup must have factory",
			"UsingTask without TaskFactory attribute cannot have ParameterGroup element",
			MSBuildDiagnosticSeverity.Error
		);

		public static MSBuildDiagnosticDescriptor TaskFactoryMustHaveBody = new MSBuildDiagnosticDescriptor (
			"TaskFactoryMustHaveBody",
			"TaskFactory must have body",
			"UsingTask with TaskFactory attribute must have Task element",
			MSBuildDiagnosticSeverity.Error
		);

		public static MSBuildDiagnosticDescriptor UnknownTaskFactory = new MSBuildDiagnosticDescriptor (
			"UnknownTaskFactory",
			"Unknown task factory",
			"The task factory '{0}' is not known",
			MSBuildDiagnosticSeverity.Warning
		);

		public static MSBuildDiagnosticDescriptor EmptyTaskFactory = new MSBuildDiagnosticDescriptor (
			"EmptyTaskFactory",
			"Empty task factory",
			"TaskFactory attribute is empty",
			MSBuildDiagnosticSeverity.Error
		);

		public static MSBuildDiagnosticDescriptor RoslynCodeTaskFactoryRequiresCodeElement = new MSBuildDiagnosticDescriptor (
			"RoslynCodeTaskFactoryRequiresCodeElement",
			"RoslynCodeTaskFactory requires Code element",
			"RoslynCodeTaskFactory requires Code element in Task body",
			MSBuildDiagnosticSeverity.Error
		);

		public static MSBuildDiagnosticDescriptor RoslynCodeTaskFactoryWithClassIgnoresParameterGroup = new MSBuildDiagnosticDescriptor (
			"RoslynCodeTaskFactoryWithClassIgnoresParameterGroup",
			"Empty task factory",
			"TaskFactory attribute is empty",
			MSBuildDiagnosticSeverity.Error);

		public static MSBuildDiagnosticDescriptor UnexpectedList = new MSBuildDiagnosticDescriptor (
			"UnexpectedList",
			"Unexpected list in value",
			"The {0} '{1}' does not permit lists",
			MSBuildDiagnosticSeverity.Warning);

		public static MSBuildDiagnosticDescriptor UnexpectedExpression = new MSBuildDiagnosticDescriptor (
			"UnexpectedExpression",
			"Unexpected expression in value",
			"The {0} '{1}' does not permit expressions",
			MSBuildDiagnosticSeverity.Warning);

		public static MSBuildDiagnosticDescriptor ImportVersionRequiresSdk = new MSBuildDiagnosticDescriptor (
			"ImportVersionRequiresSdk",
			"Import Version requires Sdk",
			"Import may only have a Version attribute if it has an Sdk attribute",
			MSBuildDiagnosticSeverity.Error);

		public static MSBuildDiagnosticDescriptor ImportMinVersionRequiresSdk = new MSBuildDiagnosticDescriptor (
			"ImportMinVersionRequiresSdk",
			"Import MinVersion requires Sdk",
			"Import may only have a MinVersion attribute if it has an Sdk attribute",
			MSBuildDiagnosticSeverity.Warning);

		public static MSBuildDiagnosticDescriptor UnknownValue = new MSBuildDiagnosticDescriptor (
			"UnknownValue",
			"{1} has unknown value",
			"{0} '{1}' has unknown value '{2}'",
			MSBuildDiagnosticSeverity.Error);

		public static MSBuildDiagnosticDescriptor HasDefaultValue = new MSBuildDiagnosticDescriptor (
			"HasDefaultValue",
			"{1} has default value",
			"{0} '{1}' has default value '{2}'",
			MSBuildDiagnosticSeverity.Warning);

		public static MSBuildDiagnosticDescriptor InvalidGuid = new MSBuildDiagnosticDescriptor (
			"InvalidGuid",
			"Invalid GUID format",
			"The value '{0}' is not a valid GUID format",
			MSBuildDiagnosticSeverity.Error);

		public static MSBuildDiagnosticDescriptor InvalidInteger = new MSBuildDiagnosticDescriptor (
			"InvalidInteger",
			"Invalid integer",
			"The value '{0}' is not a valid integer",
			MSBuildDiagnosticSeverity.Error);

		public static MSBuildDiagnosticDescriptor InvalidBool = new MSBuildDiagnosticDescriptor (
			"InvalidBool",
			"Invalid bool",
			"The value '{0}' is not a valid bool",
			MSBuildDiagnosticSeverity.Error);

		public static MSBuildDiagnosticDescriptor InvalidLcid = new MSBuildDiagnosticDescriptor (
			"InvalidLcid",
			"Invalid LCID",
			"The value '{0}' is not a valid LCID integer",
			MSBuildDiagnosticSeverity.Error);

		public static MSBuildDiagnosticDescriptor UnknownLcid = new MSBuildDiagnosticDescriptor (
			"UnknownLcid",
			"Unknown LCID",
			"The value '{0}' is not a known LCID",
			MSBuildDiagnosticSeverity.Error);

		public static MSBuildDiagnosticDescriptor InvalidUrl = new MSBuildDiagnosticDescriptor (
			"InvalidUrl",
			"Invalid URL",
			"The value '{0}' is not a valid URL",
			MSBuildDiagnosticSeverity.Error);

		public static MSBuildDiagnosticDescriptor InvalidVersion = new MSBuildDiagnosticDescriptor (
			"InvalidUrl",
			"Invalid version format",
			"The value '{0}' is not a valid version format",
			MSBuildDiagnosticSeverity.Error);

		public static MSBuildDiagnosticDescriptor UnknownTargetFramework = new MSBuildDiagnosticDescriptor (
			"UnknownTargetFramework",
			"Unknown target framework",
			"The value '{0}' is not a known target framework short name",
			MSBuildDiagnosticSeverity.Warning);

		public static MSBuildDiagnosticDescriptor UnknownTargetFrameworkIdentifier = new MSBuildDiagnosticDescriptor (
			"UnknownTargetFrameworkIdentifier",
			"Unknown target framework identifier",
			"The value '{0}' is not a known target framework identifier",
			MSBuildDiagnosticSeverity.Warning);

		public static MSBuildDiagnosticDescriptor UnknownTargetFrameworkVersion = new MSBuildDiagnosticDescriptor (
			"UnknownTargetFrameworkVersion",
			"Unknown target framework version",
			"The value '{0}' is not a known version for target framework '{1}'",
			MSBuildDiagnosticSeverity.Warning);

		public static MSBuildDiagnosticDescriptor UnknownTargetFrameworkProfile = new MSBuildDiagnosticDescriptor (
			"UnknownTargetFrameworkProfile",
			"Unknown target framework profile",
			"The value '{0}' is not a known profile for target framework '{1},Version={2}'",
			MSBuildDiagnosticSeverity.Warning);

		public static MSBuildDiagnosticDescriptor ItemAttributeNotValidInTarget = new MSBuildDiagnosticDescriptor (
			"ItemAttributeNotValidInTarget",
			"{0} not valid in targets",
			"The item attribute '{0}' is not valid in a target",
			MSBuildDiagnosticSeverity.Error);

		public static MSBuildDiagnosticDescriptor ItemAttributeOnlyValidInTarget = new MSBuildDiagnosticDescriptor (
			"ItemAttributeOnlyValidInTarget",
			"{0} not valid outside targets",
			"The item attribute '{0}' is not valid outside targets",
			MSBuildDiagnosticSeverity.Error);

		public static MSBuildDiagnosticDescriptor ItemMustHaveInclude = new MSBuildDiagnosticDescriptor (
			"ItemMustHaveInclude",
			"Item has no Include, Update or Remove attribute",
			"Items outside targets must have Include, Update or Remove attribute",
			MSBuildDiagnosticSeverity.Error);

		public static (MSBuildDiagnosticDescriptor, object[]) GetExpressionError (ExpressionError error, ValueInfo info)
		{
			(MSBuildDiagnosticDescriptor, object[]) Return (MSBuildDiagnosticDescriptor desc, params object[] args) => (desc, args);
			return error.Kind switch
			{
				ExpressionErrorKind.MetadataDisallowed => Return (MetadataDisallowed, DescriptionFormatter.GetKindNoun (info), info.Name),
				ExpressionErrorKind.EmptyListEntry => Return (EmptyListValue),
				ExpressionErrorKind.ExpectingItemName => Return (ExpectingItemName),
				ExpressionErrorKind.ExpectingRightParen => Return (ExpectingChars1, ')'),
				ExpressionErrorKind.ExpectingRightParenOrPeriod => Return (ExpectingChars2, ')', '.'),
				ExpressionErrorKind.ExpectingPropertyName => Return (ExpectingPropertyName),
				ExpressionErrorKind.ExpectingMetadataName => Return (ExpectingMetadataName),
				ExpressionErrorKind.ExpectingMetadataOrItemName => Return (ExpectingMetadataOrItemName),
				ExpressionErrorKind.ExpectingRightAngleBracket => Return (ExpectingChars1, '>'),
				ExpressionErrorKind.ExpectingRightParenOrDash => Return (ExpectingChars2, ')', '-'),
				ExpressionErrorKind.ItemsDisallowed => Return (ItemsDisallowed, DescriptionFormatter.GetKindNoun (info), info.Name),
				ExpressionErrorKind.ExpectingMethodOrTransform => Return (ExpectingFunctionOrTransform),
				ExpressionErrorKind.ExpectingMethodName => Return (ExpectingFunctionName),
				ExpressionErrorKind.ExpectingLeftParen => Return (ExpectingChars1, '('),
				ExpressionErrorKind.ExpectingRightParenOrComma => Return (ExpectingChars2, ')', ','),
				ExpressionErrorKind.ExpectingRightParenOrValue => Return (ExpectingRightParenOrValue),
				ExpressionErrorKind.ExpectingValue => Return (ExpectingValue),
				ExpressionErrorKind.CouldNotParseNumber => Return (CouldNotParseNumber),
				ExpressionErrorKind.IncompleteValue => Return (IncompleteValue),
				ExpressionErrorKind.ExpectingBracketColonColon => Return (ExpectingChars1, "]::"),
				ExpressionErrorKind.ExpectingClassName => Return (ExpectingClassName),
				ExpressionErrorKind.ExpectingClassNameComponent => Return (IncompleteClassName),
				ExpressionErrorKind.IncompleteString => Return (IncompleteString),
				ExpressionErrorKind.IncompleteProperty => Return (IncompleteProperty),
				_ => throw new System.Exception ($"Unhandled ExpressionErrorKind '{error.Kind}'")
			};
		}

		public static MSBuildDiagnosticDescriptor MetadataDisallowed = new MSBuildDiagnosticDescriptor (
			"MetadataDisallowed",
			"Metadata not permitted",
			"{0} '{1}' does not permit metadata",
			MSBuildDiagnosticSeverity.Error);

		public static MSBuildDiagnosticDescriptor ItemsDisallowed = new MSBuildDiagnosticDescriptor (
			"EmptyListEntry",
			"Items not permitted",
			"{0} '{1}' does not permit items",
			MSBuildDiagnosticSeverity.Error);

		public static MSBuildDiagnosticDescriptor EmptyListValue = new MSBuildDiagnosticDescriptor (
			"EmptyListEntry",
			"Empty list value",
			MSBuildDiagnosticSeverity.Error);

		public static MSBuildDiagnosticDescriptor ExpectingItemName = new MSBuildDiagnosticDescriptor (
			"ExpectingItemName",
			"Expecting item name",
			MSBuildDiagnosticSeverity.Error);

		public static MSBuildDiagnosticDescriptor ExpectingChars1 = new MSBuildDiagnosticDescriptor (
			"ExpectingChars",
			"Expecting '{0}'",
			MSBuildDiagnosticSeverity.Error);

		public static MSBuildDiagnosticDescriptor ExpectingChars2 = new MSBuildDiagnosticDescriptor (
			"ExpectingChars",
			"Expecting '{0}' or {1}",
			MSBuildDiagnosticSeverity.Error);

		public static MSBuildDiagnosticDescriptor ExpectingPropertyName= new MSBuildDiagnosticDescriptor (
			"ExpectingPropertyName",
			"Expecting property name",
			MSBuildDiagnosticSeverity.Error);

		public static MSBuildDiagnosticDescriptor ExpectingMetadataName = new MSBuildDiagnosticDescriptor (
			"ExpectingMetadataName",
			"Expecting metadata name",
			MSBuildDiagnosticSeverity.Error);

		public static MSBuildDiagnosticDescriptor ExpectingMetadataOrItemName = new MSBuildDiagnosticDescriptor (
			"ExpectingMetadataOrItemName",
			"Expecting metadata or item name",
			MSBuildDiagnosticSeverity.Error);

		public static MSBuildDiagnosticDescriptor ExpectingFunctionName = new MSBuildDiagnosticDescriptor (
			"ExpectingFunctionName",
			"Expecting function name",
			MSBuildDiagnosticSeverity.Error);

		public static MSBuildDiagnosticDescriptor ExpectingValue = new MSBuildDiagnosticDescriptor (
			"ExpectingValue",
			"Expecting value",
			MSBuildDiagnosticSeverity.Error);

		public static MSBuildDiagnosticDescriptor ExpectingFunctionOrTransform = new MSBuildDiagnosticDescriptor (
			"ExpectingFunctionOrTransform",
			"Expecting item function or transform",
			MSBuildDiagnosticSeverity.Error);

		public static MSBuildDiagnosticDescriptor ExpectingClassName = new MSBuildDiagnosticDescriptor (
			"ExpectingClassName",
			"Expecting class name",
			MSBuildDiagnosticSeverity.Error);

		public static MSBuildDiagnosticDescriptor IncompleteClassName = new MSBuildDiagnosticDescriptor (
			"IncompleteClassName",
			"Incomplete class name",
			MSBuildDiagnosticSeverity.Error);

		public static MSBuildDiagnosticDescriptor IncompleteString = new MSBuildDiagnosticDescriptor (
			"IncompleteString",
			"Incomplete string",
			MSBuildDiagnosticSeverity.Error);

		public static MSBuildDiagnosticDescriptor IncompleteValue = new MSBuildDiagnosticDescriptor (
			"IncompleteValue",
			"Incomplete value",
			MSBuildDiagnosticSeverity.Error);

		public static MSBuildDiagnosticDescriptor IncompleteProperty = new MSBuildDiagnosticDescriptor (
			"IncompleteProperty",
			"Incomplete property",
			MSBuildDiagnosticSeverity.Error);

		public static MSBuildDiagnosticDescriptor CouldNotParseNumber = new MSBuildDiagnosticDescriptor (
			"CouldNotParseNumber",
			"Invalid number format",
			MSBuildDiagnosticSeverity.Error);

		public static MSBuildDiagnosticDescriptor ExpectingRightParenOrValue = new MSBuildDiagnosticDescriptor (
			"ExpectingRightParenOrValue",
			"Expecting ')' or value",
			MSBuildDiagnosticSeverity.Error);

		public const string UnwrittenItemId = nameof (UnwrittenItem);
		public static MSBuildDiagnosticDescriptor UnwrittenItem = new MSBuildDiagnosticDescriptor (
			UnwrittenItemId,
			"Possible unused or misspelled item",
			"The item '{0}' does not have a value assigned and is not referenced in any imported targets or schemas",
			MSBuildDiagnosticSeverity.Warning);

		public const string UnwrittenPropertyId = nameof (UnwrittenProperty);
		public static MSBuildDiagnosticDescriptor UnwrittenProperty = new MSBuildDiagnosticDescriptor (
			UnwrittenPropertyId,
			"Possible unused or misspelled property",
			"The property '{0}' does not have a value assigned and is not referenced in any imported targets or schemas",
			MSBuildDiagnosticSeverity.Warning);

		public const string UnwrittenMetadataId = nameof (UnwrittenMetadata);
		public static MSBuildDiagnosticDescriptor UnwrittenMetadata = new MSBuildDiagnosticDescriptor (
			UnwrittenMetadataId,
			"Possible unused or misspelled metadata",
			"The metadata '{0}.{1}' does not have a value assigned and is not referenced in any imported targets or schemas",
			MSBuildDiagnosticSeverity.Warning);

		public const string UnreadItemId = nameof (UnreadItem);
		public static MSBuildDiagnosticDescriptor UnreadItem = new MSBuildDiagnosticDescriptor (
			UnreadItemId,
			"Possible unused or misspelled item",
			"The item '{0}' is not used in this file and is referenced in any imported targets or schemas",
			MSBuildDiagnosticSeverity.Warning);

		public const string UnreadPropertyId = nameof (UnreadProperty);
		public static MSBuildDiagnosticDescriptor UnreadProperty = new MSBuildDiagnosticDescriptor (
			UnreadPropertyId,
			"Possible unused or misspelled property",
			"The property '{0}' is not used in this file and is not referenced in any imported targets or schemas",
			MSBuildDiagnosticSeverity.Warning);

		public const string UnreadMetadataId = nameof (UnreadMetadata);
		public static MSBuildDiagnosticDescriptor UnreadMetadata = new MSBuildDiagnosticDescriptor (
			UnreadMetadataId,
			"Possible unused or misspelled metadata",
			"The metadata '{0}.{1}' is not used in this file and is not referenced in any imported targets or schemas",
			MSBuildDiagnosticSeverity.Warning);
	}
}
