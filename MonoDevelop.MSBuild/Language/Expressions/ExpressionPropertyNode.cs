// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MonoDevelop.MSBuild.Language.Expressions
{
	class ExpressionPropertyNode : ExpressionNode
	{
		public ExpressionPropertyNode(int offset, int length) : base (offset, length)
		{
		}
	}

	class ExpressionPropertyFunctionInvocation : ExpressionPropertyNode
	{
		public ExpressionPropertyNode Target { get; }
		public ExpressionFunctionName Function { get; }
		public ExpressionArgumentList Arguments;

		public bool IsProperty => Function != null && Arguments == null;
		public bool IsIndexer => Function == null && Arguments != null;

		public ExpressionPropertyFunctionInvocation (int offset, int length, ExpressionPropertyNode target, ExpressionFunctionName function, ExpressionArgumentList arguments)
			: base (offset, length)
		{
			Target = target;
			target?.SetParent (this);
			Function = function;
			function?.SetParent (this);
			Arguments = arguments;
			arguments?.SetParent (this);
		}
	}

	class ExpressionPropertyName : ExpressionPropertyNode
	{
		public string Name { get; }

		public ExpressionPropertyName (int offset, int length, string name) : base (offset, length)
		{
			Name = name;
		}
	}

	class ExpressionPropertyRegistryValue : ExpressionPropertyNode
	{
		public string RegistryReference { get; }

		public ExpressionPropertyRegistryValue (int offset, int length, string registryReference) : base (offset, length)
		{
			RegistryReference = registryReference;
		}
	}

	class ExpressionClassReference : ExpressionPropertyNode
	{
		public string Name { get; }

		public ExpressionClassReference (int offset, int length, string name) : base (offset, length)
		{
			Name = name;
		}
	}
}
