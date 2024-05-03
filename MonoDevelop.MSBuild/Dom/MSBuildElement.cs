// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Language.Syntax;
using MonoDevelop.MSBuild.Language.Typesystem;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Dom
{
	public abstract class MSBuildElement : MSBuildObject
	{
		readonly MSBuildElement firstChild;
		readonly MSBuildAttribute firstAttribute;
		MSBuildElement nextSibling;

		internal MSBuildElement (MSBuildElement parent, XElement xelement, ExpressionNode value)
			: base (parent, value)
		{
			XElement = xelement;
			Debug.Assert (xelement.IsNamed && !xelement.Name.HasPrefix);

			MSBuildElement prevChild = null;
			foreach (var childElement in xelement.Elements) {
				if (childElement.Name.HasPrefix || Syntax.GetChild (childElement.Name.Name) is not MSBuildElementSyntax childSyntax) {
					continue;
				}
				ExpressionNode childValue = null;
				if (childSyntax.ValueKind != MSBuildValueKind.Nothing && childElement.FirstChild is XText t) {
					childValue = ExpressionParser.Parse (t.Text, ExpressionOptions.ItemsMetadataAndLists, t.Span.Start);
				}
				var child = CreateElement (childSyntax.SyntaxKind, this, childElement, childValue);
				if (prevChild == null) {
					firstChild = child;
				} else {
					prevChild.nextSibling = child;
				}
				prevChild = child;
			}

			MSBuildAttribute prevAttribute = null;
			foreach (var xatt in xelement.Attributes) {
				if (Syntax.GetAttribute (xatt) is not MSBuildAttributeSyntax attributeSyntax) {
					continue;
				}
				ExpressionNode attributeValue = null;
				if (xatt.HasNonEmptyValue) {
					if (attributeSyntax.ValueKind == MSBuildValueKind.Condition) {
						attributeValue = ExpressionParser.ParseCondition (xatt.Value, xatt.ValueOffset.Value);
					} else {
						attributeValue = ExpressionParser.Parse (xatt.Value, ExpressionOptions.ItemsMetadataAndLists, xatt.ValueOffset.Value);
					}
				}

				var attribute = new MSBuildAttribute (this, xatt, attributeSyntax, attributeValue);
				if (prevAttribute == null) {
					firstAttribute = attribute;
				} else {
					prevAttribute.nextSibling = attribute;
				}
				prevAttribute = attribute;
			}
		}

		protected virtual void OnAddAttribute (MSBuildAttribute attribute) { }

		public abstract MSBuildElementSyntax Syntax { get; }
		public XElement XElement { get; }

		public string ElementName => XElement.Name.Name;

		public bool IsElementNamed (string name) => string.Equals (ElementName, name, StringComparison.OrdinalIgnoreCase);

		public override MSBuildSyntaxKind SyntaxKind => Syntax.SyntaxKind;

		public IEnumerable<MSBuildElement> Elements {
			get {
				var el = firstChild;
				while (el != null) {
					yield return el;
					el = el.nextSibling;
				}
			}
		}

		public IEnumerable<MSBuildElement> FollowingSiblings {
			get {
				var el = nextSibling;
				while (el != null) {
					yield return el;
					el = el.nextSibling;
				}
			}
		}

		public IEnumerable<MSBuildAttribute> Attributes {
			get {
				var att = firstAttribute;
				while (att != null) {
					yield return att;
					att = att.nextSibling;
				}
			}
		}

		public MSBuildElement GetElement (MSBuildSyntaxKind kind)
		{
			var el = firstChild;
			while (el != null) {
				if (el.SyntaxKind == kind) {
					return el;
				}
				el = el.nextSibling;
			}
			return null;
		}

		// consumers should get elements by kind, not name
		// but MSBuildItemElement uses this for GetMetadataElement
		protected T GetElement<T> (string elementName) where T : MSBuildElement
		{
			var element = firstChild;
			while (element != null) {
				if (element.XElement.Name.Equals (elementName, true) && element is T typedElement) {
					return typedElement;
				}
				element = element.nextSibling;
			}
			return null;
		}

		public IEnumerable<MSBuildElement> GetElements (MSBuildSyntaxKind kind)
		{
			var el = firstChild;
			while (el != null) {
				if (el.SyntaxKind == kind) {
					yield return el;
				}
				el = el.nextSibling;
			}
		}

		public IEnumerable<T> GetElements<T> () where T : MSBuildElement
		{
			var el = firstChild;
			while (el != null) {
				if (el is T t) {
					yield return t;
				}
				el = el.nextSibling;
			}
		}

		public MSBuildAttribute GetAttribute (MSBuildSyntaxKind kind)
		{
			var att = firstAttribute;
			while (att != null) {
				if (att.SyntaxKind == kind) {
					return att;
				}
				att = att.nextSibling;
			}
			return null;
		}

		// consumers should get attributes by kind, not name
		// but MSBuildItemElement uses this for GetMetadataAttribute
		protected MSBuildAttribute GetAttribute (string attributeName)
		{
			var att = firstAttribute;
			while (att != null) {
				if (string.Equals (att.Name, attributeName, StringComparison.OrdinalIgnoreCase)) {
					return att;
				}
				att = att.nextSibling;
			}
			return null;
		}

		public IEnumerable<MSBuildAttribute> GetAttributes (MSBuildSyntaxKind kind)
		{
			var att = firstAttribute;
			while (att != null) {
				if (att.SyntaxKind == kind) {
					yield return att;
				}
				att = att.nextSibling;
			}
		}

		static MSBuildElement CreateElement (MSBuildSyntaxKind syntaxKind, MSBuildElement parent, XElement xelement, ExpressionNode value)
			=> syntaxKind switch
			{
				MSBuildSyntaxKind.Choose => new MSBuildChooseElement (parent, xelement, value),
				MSBuildSyntaxKind.Import => new MSBuildImportElement (parent, xelement, value),
				MSBuildSyntaxKind.ImportGroup => new MSBuildImportGroupElement (parent, xelement, value),
				MSBuildSyntaxKind.Item => new MSBuildItemElement (parent, xelement, value),
				MSBuildSyntaxKind.ItemDefinitionGroup => new MSBuildItemDefinitionGroupElement (parent, xelement, value),
				MSBuildSyntaxKind.ItemDefinition => new MSBuildItemDefinitionElement (parent, xelement, value),
				MSBuildSyntaxKind.ItemGroup => new MSBuildItemGroupElement (parent, xelement, value),
				MSBuildSyntaxKind.Metadata => new MSBuildMetadataElement (parent, xelement, value),
				MSBuildSyntaxKind.OnError => new MSBuildOnErrorElement (parent, xelement, value),
				MSBuildSyntaxKind.Otherwise => new MSBuildOtherwiseElement (parent, xelement, value),
				MSBuildSyntaxKind.Output => new MSBuildOutputElement (parent, xelement, value),
				MSBuildSyntaxKind.Parameter => new MSBuildParameterElement (parent, xelement, value),
				MSBuildSyntaxKind.ParameterGroup => new MSBuildParameterGroupElement (parent, xelement, value),
				MSBuildSyntaxKind.Project => new MSBuildProjectElement (xelement),
				MSBuildSyntaxKind.ProjectExtensions => new MSBuildProjectExtensionsElement (parent, xelement, value),
				MSBuildSyntaxKind.Property => new MSBuildPropertyElement (parent, xelement, value),
				MSBuildSyntaxKind.PropertyGroup => new MSBuildPropertyGroupElement (parent, xelement, value),
				MSBuildSyntaxKind.Sdk => new MSBuildSdkElement (parent, xelement, value),
				MSBuildSyntaxKind.Target => new MSBuildTargetElement (parent, xelement, value),
				MSBuildSyntaxKind.Task => new MSBuildTaskElement (parent, xelement, value),
				MSBuildSyntaxKind.TaskBody => new MSBuildTaskBodyElement (parent, xelement, value),
				MSBuildSyntaxKind.UsingTask => new MSBuildUsingTaskElement (parent, xelement, value),
				MSBuildSyntaxKind.When => new MSBuildWhenElement (parent, xelement, value),
				MSBuildSyntaxKind.Unknown => null,
				_ => throw new ArgumentException ($"Unsupported MSBuildSyntaxKind {syntaxKind}", nameof (syntaxKind))
			};
	}

	public abstract class MSBuildConditionedElement : MSBuildElement
	{
		protected MSBuildConditionedElement (MSBuildElement parent, XElement xelement, ExpressionNode value) : base (parent, xelement, value) { }
		public MSBuildAttribute ConditionAttribute => GetAttribute (MSBuildSyntaxKind.ConditionAttribute | SyntaxKind);

		// not all conditioned nodes can have labels, but all labelled nodes can have conditions, so put it here anyway
		public MSBuildAttribute LabelAttribute => GetAttribute (MSBuildSyntaxKind.LabelAttribute | SyntaxKind);
	}

	public class MSBuildChooseElement : MSBuildElement
	{
		internal MSBuildChooseElement (MSBuildElement parent, XElement xelement, ExpressionNode value) : base (parent, xelement, value) { }
		public override MSBuildElementSyntax Syntax => MSBuildElementSyntax.Choose;
	}

	public class MSBuildImportElement : MSBuildConditionedElement
	{
		internal MSBuildImportElement (MSBuildElement parent, XElement xelement, ExpressionNode value) : base (parent, xelement, value) { }

		public override MSBuildElementSyntax Syntax => MSBuildElementSyntax.Import;

		public MSBuildAttribute ProjectAttribute => GetAttribute (MSBuildSyntaxKind.Import_Project);
		public MSBuildAttribute SdkAttribute => GetAttribute (MSBuildSyntaxKind.Import_Sdk);
		public MSBuildAttribute VersionAttribute => GetAttribute (MSBuildSyntaxKind.Import_Version);
		public MSBuildAttribute MinimumVersionAttribute => GetAttribute (MSBuildSyntaxKind.Import_MinimumVersion);
	}

	public class MSBuildImportGroupElement : MSBuildElement
	{
		internal MSBuildImportGroupElement (MSBuildElement parent, XElement xelement, ExpressionNode value) : base (parent, xelement, value) { }
		public override MSBuildElementSyntax Syntax => MSBuildElementSyntax.ImportGroup;
	}

	public class MSBuildItemElement : MSBuildConditionedElement
	{
		internal MSBuildItemElement (MSBuildElement parent, XElement xelement, ExpressionNode value) : base (parent, xelement, value) { }
		public override MSBuildElementSyntax Syntax => MSBuildElementSyntax.Item;

		public MSBuildAttribute ExcludeAttribute => GetAttribute (MSBuildSyntaxKind.Item_Exclude);
		public MSBuildAttribute IncludeAttribute => GetAttribute (MSBuildSyntaxKind.Item_Include);
		public MSBuildAttribute RemoveAttribute => GetAttribute (MSBuildSyntaxKind.Item_Remove);
		public MSBuildAttribute UpdateAttribute => GetAttribute (MSBuildSyntaxKind.Item_Update);
		public MSBuildAttribute KeepMetadataAttribute => GetAttribute (MSBuildSyntaxKind.Item_KeepMetadata);
		public MSBuildAttribute RemoveMetadataAttribute => GetAttribute (MSBuildSyntaxKind.Item_RemoveMetadata);
		public MSBuildAttribute KeepDuplicatesAttribute => GetAttribute (MSBuildSyntaxKind.Item_KeepDuplicates);

		public IEnumerable<MSBuildAttribute> MetadataAttributes => GetAttributes (MSBuildSyntaxKind.Item_Metadata);

		public IEnumerable<MSBuildMetadataElement> MetadataElements => GetElements<MSBuildMetadataElement> ();

		public MSBuildAttribute GetMetadataAttribute (string metadataName)
		{
			var att = GetAttribute (metadataName);
			if (att.SyntaxKind == MSBuildSyntaxKind.Item_Metadata) {
				return att;
			}
			return null;
		}

		public MSBuildMetadataElement GetMetadataElement (string metadataName) =>  GetElement<MSBuildMetadataElement> (metadataName);
	}

	public class MSBuildItemDefinitionElement : MSBuildElement
	{
		internal MSBuildItemDefinitionElement (MSBuildElement parent, XElement xelement, ExpressionNode value) : base (parent, xelement, value) { }
		public override MSBuildElementSyntax Syntax => MSBuildElementSyntax.ItemDefinition;

		public IEnumerable<MSBuildMetadataElement> MetadataElements => GetElements<MSBuildMetadataElement> ();
	}

	public class MSBuildItemDefinitionGroupElement : MSBuildConditionedElement
	{
		internal MSBuildItemDefinitionGroupElement (MSBuildElement parent, XElement xelement, ExpressionNode value) : base (parent, xelement, value) { }
		public override MSBuildElementSyntax Syntax => MSBuildElementSyntax.ItemDefinitionGroup;

		public IEnumerable<MSBuildItemDefinitionElement> ItemDefinitionElements => GetElements<MSBuildItemDefinitionElement> ();
	}

	public class MSBuildItemGroupElement : MSBuildConditionedElement
	{
		internal MSBuildItemGroupElement (MSBuildElement parent, XElement xelement, ExpressionNode value) : base (parent, xelement, value) { }
		public override MSBuildElementSyntax Syntax => MSBuildElementSyntax.ItemGroup;
		public IEnumerable<MSBuildItemElement> ItemElements => GetElements<MSBuildItemElement> ();
	}

	public class MSBuildMetadataElement : MSBuildConditionedElement
	{
		internal MSBuildMetadataElement (MSBuildElement parent, XElement xelement, ExpressionNode value) : base (parent, xelement, value) { }
		public override MSBuildElementSyntax Syntax => MSBuildElementSyntax.Metadata;
	}

	public class MSBuildOnErrorElement : MSBuildConditionedElement
	{
		internal MSBuildOnErrorElement (MSBuildElement parent, XElement xelement, ExpressionNode value) : base (parent, xelement, value) { }
		public override MSBuildElementSyntax Syntax => MSBuildElementSyntax.OnError;

		public MSBuildAttribute ExecuteTargetsAttribute => GetAttribute (MSBuildSyntaxKind.OnError_ExecuteTargets);
	}

	public class MSBuildOtherwiseElement : MSBuildElement
	{
		internal MSBuildOtherwiseElement (MSBuildElement parent, XElement xelement, ExpressionNode value) : base (parent, xelement, value) { }
		public override MSBuildElementSyntax Syntax => MSBuildElementSyntax.Otherwise;
	}

	public class MSBuildOutputElement : MSBuildElement
	{
		internal MSBuildOutputElement (MSBuildElement parent, XElement xelement, ExpressionNode value) : base (parent, xelement, value) { }
		public override MSBuildElementSyntax Syntax => MSBuildElementSyntax.Output;

		public MSBuildAttribute TaskParameterAttribute => GetAttribute (MSBuildSyntaxKind.Output_TaskParameter);
		public MSBuildAttribute ItemNameAttribute => GetAttribute (MSBuildSyntaxKind.Output_ItemName);
		public MSBuildAttribute PropertyNameAttribute => GetAttribute (MSBuildSyntaxKind.Output_PropertyName);
	}

	public class MSBuildParameterElement : MSBuildElement
	{
		internal MSBuildParameterElement (MSBuildElement parent, XElement xelement, ExpressionNode value) : base (parent, xelement, value) { }
		public override MSBuildElementSyntax Syntax => MSBuildElementSyntax.Parameter;
		public MSBuildAttribute OutputAttribute => GetAttribute (MSBuildSyntaxKind.Parameter_Output);
		public MSBuildAttribute ParameterTypeAttribute => GetAttribute (MSBuildSyntaxKind.Parameter_ParameterType);
		public MSBuildAttribute RequiredAttribute => GetAttribute (MSBuildSyntaxKind.Parameter_Required);
	}

	public class MSBuildParameterGroupElement : MSBuildElement
	{
		internal MSBuildParameterGroupElement (MSBuildElement parent, XElement xelement, ExpressionNode value) : base (parent, xelement, value) { }
		public override MSBuildElementSyntax Syntax => MSBuildElementSyntax.ParameterGroup;

		public IEnumerable<MSBuildParameterElement> ParameterElements => GetElements<MSBuildParameterElement> ();
	}

	public class MSBuildProjectElement : MSBuildElement
	{
		internal MSBuildProjectElement (XElement xelement) : base (null, xelement, null) { }
		public override MSBuildElementSyntax Syntax => MSBuildElementSyntax.Project;

		public MSBuildAttribute DefaultTargetsAttribute => GetAttribute (MSBuildSyntaxKind.Project_DefaultTargets);
		public MSBuildAttribute InitialTargetsAttribute => GetAttribute (MSBuildSyntaxKind.Project_InitialTargets);
		public MSBuildAttribute ToolsVersionAttribute => GetAttribute (MSBuildSyntaxKind.Project_ToolsVersion);
		public MSBuildAttribute TreatAsLocalPropertyAttribute => GetAttribute (MSBuildSyntaxKind.Project_TreatAsLocalProperty);
		public MSBuildAttribute XmlnsAttribute => GetAttribute (MSBuildSyntaxKind.Project_xmlns);
		public MSBuildAttribute SdkAttribute => GetAttribute (MSBuildSyntaxKind.Project_Sdk);
	}

	public class MSBuildProjectExtensionsElement : MSBuildElement
	{
		internal MSBuildProjectExtensionsElement (MSBuildElement parent, XElement xelement, ExpressionNode value) : base (parent, xelement, value) { }
		public override MSBuildElementSyntax Syntax => MSBuildElementSyntax.ProjectExtensions;
	}

	public class MSBuildPropertyElement : MSBuildConditionedElement
	{
		internal MSBuildPropertyElement (MSBuildElement parent, XElement xelement, ExpressionNode value) : base (parent, xelement, value) { }
		public override MSBuildElementSyntax Syntax => MSBuildElementSyntax.Property;
	}

	public class MSBuildPropertyGroupElement : MSBuildConditionedElement
	{
		internal MSBuildPropertyGroupElement (MSBuildElement parent, XElement xelement, ExpressionNode value) : base (parent, xelement, value) { }
		public override MSBuildElementSyntax Syntax => MSBuildElementSyntax.PropertyGroup;

		public IEnumerable<MSBuildPropertyElement> PropertyElements => GetElements<MSBuildPropertyElement> ();
	}

	public class MSBuildTargetElement : MSBuildConditionedElement
	{
		internal MSBuildTargetElement (MSBuildElement parent, XElement xelement, ExpressionNode value) : base (parent, xelement, value) { }
		public override MSBuildElementSyntax Syntax => MSBuildElementSyntax.Target;

		public MSBuildAttribute NameAttribute => GetAttribute (MSBuildSyntaxKind.Target_Name);
		public MSBuildAttribute DependsOnTargetsAttribute => GetAttribute (MSBuildSyntaxKind.Target_DependsOnTargets);
		public MSBuildAttribute InputsAttribute => GetAttribute (MSBuildSyntaxKind.Target_Inputs);
		public MSBuildAttribute OutputsAttribute => GetAttribute (MSBuildSyntaxKind.Target_Outputs);
		public MSBuildAttribute KeepDuplicateOutputsAttribute => GetAttribute (MSBuildSyntaxKind.Target_KeepDuplicateOutputs);
		public MSBuildAttribute ReturnsAttribute => GetAttribute (MSBuildSyntaxKind.Target_Returns);
		public MSBuildAttribute BeforeTargetsAttribute => GetAttribute (MSBuildSyntaxKind.Target_BeforeTargets);
		public MSBuildAttribute AfterTargetsAttribute => GetAttribute (MSBuildSyntaxKind.Target_AfterTargets);

		public IEnumerable<MSBuildTaskElement> TaskElements => GetElements<MSBuildTaskElement> ();
	}

	public class MSBuildSdkElement : MSBuildElement
	{
		internal MSBuildSdkElement (MSBuildElement parent, XElement xelement, ExpressionNode value) : base (parent, xelement, value) { }
		public override MSBuildElementSyntax Syntax => MSBuildElementSyntax.Sdk;

		public MSBuildAttribute NameAttribute => GetAttribute (MSBuildSyntaxKind.Sdk_Name);
		public MSBuildAttribute Version => GetAttribute (MSBuildSyntaxKind.Sdk_Version);
		public MSBuildAttribute MinimumVersion => GetAttribute (MSBuildSyntaxKind.Sdk_MinimumVersion);
	}

	public class MSBuildTaskElement : MSBuildConditionedElement
	{
		internal MSBuildTaskElement (MSBuildElement parent, XElement xelement, ExpressionNode value) : base (parent, xelement, value) { }
		public override MSBuildElementSyntax Syntax => MSBuildElementSyntax.Task;

		public MSBuildAttribute ContinueOnErrorAttribute => GetAttribute (MSBuildSyntaxKind.Task_ContinueOnError);
		public MSBuildAttribute ArchitectureAttribute => GetAttribute (MSBuildSyntaxKind.Task_Architecture);
		public MSBuildAttribute RuntimeAttribute => GetAttribute (MSBuildSyntaxKind.Task_Runtime);

		public IEnumerable<MSBuildAttribute> ParametersAttribute => GetAttributes (MSBuildSyntaxKind.Task_Parameter);
		public IEnumerable<MSBuildOutputElement> OutputElements => GetElements<MSBuildOutputElement> ();

	}

	public class MSBuildTaskBodyElement : MSBuildElement
	{
		internal MSBuildTaskBodyElement (MSBuildElement parent, XElement xelement, ExpressionNode value) : base (parent, xelement, value) { }
		public override MSBuildElementSyntax Syntax => MSBuildElementSyntax.TaskBody;

		public MSBuildAttribute EvaluateAttribute => GetAttribute (MSBuildSyntaxKind.UsingTaskBody_Evaluate);
	}

	public class MSBuildUsingTaskElement : MSBuildConditionedElement
	{
		internal MSBuildUsingTaskElement (MSBuildElement parent, XElement xelement, ExpressionNode value) : base (parent, xelement, value) { }
		public override MSBuildElementSyntax Syntax => MSBuildElementSyntax.UsingTask;

		public MSBuildAttribute AssemblyNameAttribute => GetAttribute (MSBuildSyntaxKind.UsingTask_AssemblyName);
		public MSBuildAttribute AssemblyFileAttribute => GetAttribute (MSBuildSyntaxKind.UsingTask_AssemblyFile);
		public MSBuildAttribute TaskNameAttribute => GetAttribute (MSBuildSyntaxKind.UsingTask_TaskName);
		public MSBuildAttribute TaskFactoryAttribute => GetAttribute (MSBuildSyntaxKind.UsingTask_TaskFactory);
		public MSBuildAttribute ArchitectureAttribute => GetAttribute (MSBuildSyntaxKind.UsingTask_Architecture);
		public MSBuildAttribute RuntimeAttribute => GetAttribute (MSBuildSyntaxKind.UsingTask_Runtime);

		public MSBuildTaskBodyElement TaskBody => GetElements<MSBuildTaskBodyElement> ().FirstOrDefault ();
		public MSBuildParameterGroupElement ParameterGroup => GetElements<MSBuildParameterGroupElement> ().FirstOrDefault ();

	}

	public class MSBuildWhenElement : MSBuildConditionedElement
	{
		internal MSBuildWhenElement (MSBuildElement parent, XElement xelement, ExpressionNode value) : base (parent, xelement, value) { }
		public override MSBuildElementSyntax Syntax => MSBuildElementSyntax.When;
	}
}
