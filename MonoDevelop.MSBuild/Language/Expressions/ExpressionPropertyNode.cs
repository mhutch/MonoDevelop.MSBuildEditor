// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MonoDevelop.MSBuild.Language.Expressions
{
	abstract class ExpressionPropertyNode : ExpressionNode
	{
		public ExpressionPropertyNode(int offset, int length) : base (offset, length)
		{
		}
	}

	class ExpressionPropertyFunctionInvocation : ExpressionPropertyNode
	{
		public ExpressionNode Target { get; }
		public ExpressionFunctionName Function { get; }
		public ExpressionNode Arguments;

		public bool IsProperty => Function != null && Arguments == null;
		public bool IsIndexer => Function == null && Arguments != null;

		public ExpressionPropertyFunctionInvocation (int offset, int length, ExpressionNode target, ExpressionFunctionName function, ExpressionNode arguments)
			: base (offset, length)
		{
			Target = target;
			target?.SetParent (this);
			Function = function;
			function?.SetParent (this);
			Arguments = arguments;
			arguments?.SetParent (this);
		}

		public override ExpressionNodeKind NodeKind => ExpressionNodeKind.PropertyFunctionInvocation;
	}

	class ExpressionPropertyName : ExpressionPropertyNode
	{
		public string Name { get; }

		public ExpressionPropertyName (int offset, string name) : base (offset, name.Length)
		{
			Name = name;
		}

		public override ExpressionNodeKind NodeKind => ExpressionNodeKind.PropertyName;
	}

	class ExpressionPropertyRegistryValue : ExpressionPropertyNode
	{
		public string RegistryReference { get; }

		public ExpressionPropertyRegistryValue (int offset, int length, string registryReference) : base (offset, length)
		{
			RegistryReference = registryReference;
		}

		public override ExpressionNodeKind NodeKind => ExpressionNodeKind.PropertyRegistryValue;
	}

	class ExpressionClassReference : ExpressionPropertyNode
	{
		public string Name { get; }

		public ExpressionClassReference (int offset, string name) : base (offset, name.Length)
		{
			Name = name;
		}

		public override ExpressionNodeKind NodeKind => ExpressionNodeKind.ClassReference;
	}
}
