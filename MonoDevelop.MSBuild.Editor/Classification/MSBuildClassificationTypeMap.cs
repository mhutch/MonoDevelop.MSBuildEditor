// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;

using MonoDevelop.MSBuild.Language.Expressions;

namespace MonoDevelop.MSBuild.Editor.Classification
{
	/// <summary>
	/// Maps MSBuild syntax constructs to classification tags, for use by <see cref="MSBuildClassificationTagger"/>
	/// when the VS TextMate service is unavailable.
	/// XML constructs prefer the classification types registered by Visual Studio's built-in XML editor,
	/// so files look exactly like the XML editor (including user Fonts &amp; Colors customizations),
	/// falling back to built-in theme-aware types in hosts that do not register them.
	/// </summary>
	[Export]
	sealed class MSBuildClassificationTypeMap
	{
		// classification type names registered by Visual Studio's built-in XML editor
		const string XmlNameTypeName = "XML Name";
		const string XmlAttributeTypeName = "XML Attribute";
		const string XmlAttributeValueTypeName = "XML Attribute Value";
		const string XmlAttributeQuotesTypeName = "XML Attribute Quotes";
		const string XmlDelimiterTypeName = "XML Delimiter";
		const string XmlCommentTypeName = "XML Comment";
		const string XmlCDataSectionTypeName = "XML CData Section";
		const string XmlTextTypeName = "XML Text";
		const string XmlProcessingInstructionTypeName = "XML Processing Instruction";

		/// <summary>
		/// Creates the map, resolving classification types from the registry.
		/// </summary>
		/// <param name="classificationTypeRegistry">The editor's classification type registry.</param>
		[ImportingConstructor]
		public MSBuildClassificationTypeMap (IClassificationTypeRegistryService classificationTypeRegistry)
		{
			ElementName = CreateTag (classificationTypeRegistry, XmlNameTypeName, PredefinedClassificationTypeNames.MarkupNode);
			AttributeName = CreateTag (classificationTypeRegistry, XmlAttributeTypeName, PredefinedClassificationTypeNames.MarkupAttribute);
			AttributeValue = CreateTag (classificationTypeRegistry, XmlAttributeValueTypeName, PredefinedClassificationTypeNames.String);
			AttributeQuotes = CreateTag (classificationTypeRegistry, XmlAttributeQuotesTypeName, PredefinedClassificationTypeNames.String);
			Delimiter = CreateTag (classificationTypeRegistry, XmlDelimiterTypeName, PredefinedClassificationTypeNames.Operator);
			Comment = CreateTag (classificationTypeRegistry, XmlCommentTypeName, PredefinedClassificationTypeNames.Comment);
			CDataSection = CreateTag (classificationTypeRegistry, XmlCDataSectionTypeName, PredefinedClassificationTypeNames.Literal);
			Text = CreateTag (classificationTypeRegistry, XmlTextTypeName, PredefinedClassificationTypeNames.Text);
			ProcessingInstruction = CreateTag (classificationTypeRegistry, XmlProcessingInstructionTypeName, PredefinedClassificationTypeNames.PreprocessorKeyword);
			// the XML editor has no dedicated entity reference classification, it uses the name color
			EntityReference = CreateTag (classificationTypeRegistry, XmlNameTypeName, PredefinedClassificationTypeNames.MarkupNode);

			ExpressionName = CreateTag (classificationTypeRegistry, PredefinedClassificationTypeNames.Keyword);
			ExpressionDelimiter = CreateTag (classificationTypeRegistry, PredefinedClassificationTypeNames.Operator);
			FunctionName = CreateTag (classificationTypeRegistry, PredefinedClassificationTypeNames.Identifier);
			BoolLiteral = CreateTag (classificationTypeRegistry, PredefinedClassificationTypeNames.Keyword);
			NumberLiteral = CreateTag (classificationTypeRegistry, PredefinedClassificationTypeNames.Number);

			ResolvedTypeNames =
				$"elementName='{ElementName.ClassificationType.Classification}', " +
				$"attributeName='{AttributeName.ClassificationType.Classification}', " +
				$"attributeValue='{AttributeValue.ClassificationType.Classification}', " +
				$"attributeQuotes='{AttributeQuotes.ClassificationType.Classification}', " +
				$"delimiter='{Delimiter.ClassificationType.Classification}', " +
				$"comment='{Comment.ClassificationType.Classification}', " +
				$"cdata='{CDataSection.ClassificationType.Classification}', " +
				$"text='{Text.ClassificationType.Classification}', " +
				$"processingInstruction='{ProcessingInstruction.ClassificationType.Classification}'";
		}

		public ClassificationTag ElementName { get; }
		public ClassificationTag AttributeName { get; }
		public ClassificationTag AttributeValue { get; }
		public ClassificationTag AttributeQuotes { get; }
		public ClassificationTag Delimiter { get; }
		public ClassificationTag Comment { get; }
		public ClassificationTag CDataSection { get; }
		public ClassificationTag Text { get; }
		public ClassificationTag ProcessingInstruction { get; }
		public ClassificationTag EntityReference { get; }
		public ClassificationTag ExpressionName { get; }
		public ClassificationTag ExpressionDelimiter { get; }
		public ClassificationTag FunctionName { get; }
		public ClassificationTag BoolLiteral { get; }
		public ClassificationTag NumberLiteral { get; }

		/// <summary>
		/// Describes which classification type each XML bucket resolved to, for logging purposes.
		/// </summary>
		public string ResolvedTypeNames { get; }

		/// <summary>
		/// Gets the classification tag for an MSBuild expression node, or null if the node is not classified.
		/// </summary>
		/// <param name="node">The expression node.</param>
		/// <returns>The tag for the node's whole span, or null to leave the span unclassified.</returns>
		public ClassificationTag? GetTagForExpressionNode (ExpressionNode node)
			=> node switch {
				ExpressionPropertyName => ExpressionName,
				ExpressionItemName => ExpressionName,
				ExpressionFunctionName => FunctionName,
				ExpressionArgumentBool => BoolLiteral,
				ExpressionArgumentInt => NumberLiteral,
				ExpressionArgumentFloat => NumberLiteral,
				ExpressionArgumentString => AttributeValue,
				_ => null
			};

		/// <summary>
		/// Creates a classification tag for the first classification type name that resolves in the registry,
		/// falling back to plain text if none is registered.
		/// </summary>
		/// <param name="classificationTypeRegistry">The editor's classification type registry.</param>
		/// <param name="classificationTypeNames">Candidate classification type names, in order of preference.</param>
		/// <returns>A tag for the resolved classification type.</returns>
		static ClassificationTag CreateTag (IClassificationTypeRegistryService classificationTypeRegistry, params string[] classificationTypeNames)
		{
			foreach (string classificationTypeName in classificationTypeNames) {
				IClassificationType? classificationType = classificationTypeRegistry.GetClassificationType (classificationTypeName);
				if (classificationType is not null) {
					return new ClassificationTag (classificationType);
				}
			}
			return new ClassificationTag (classificationTypeRegistry.GetClassificationType (PredefinedClassificationTypeNames.Text));
		}
	}
}
