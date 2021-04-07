// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

using MonoDevelop.MSBuild.Schema;

namespace MonoDevelop.MSBuild.Language.Typesystem
{
	class FunctionInfo : BaseSymbol
	{
		readonly FunctionParameterInfo [] arguments;

		public virtual MSBuildValueKind ReturnType { get; }
		public string ReturnTypeString => string.Join (" ", ReturnType.GetTypeDescription ());
		public virtual FunctionParameterInfo [] Parameters => arguments;
		public List<FunctionInfo> Overloads { get; } = new List<FunctionInfo> ();
		public virtual bool IsProperty => false;

		protected FunctionInfo (string name, DisplayText description) : base (name, description)
		{
		}

		public FunctionInfo (string name, DisplayText description, MSBuildValueKind returnType, params FunctionParameterInfo [] arguments) : base (name, description)
		{
			this.arguments = arguments;
			this.ReturnType = returnType;
		}
	}

	class FunctionParameterInfo : BaseSymbol
	{
		readonly string type;

		public virtual string Type => type;

		protected FunctionParameterInfo (string name, DisplayText description) : base (name, description)
		{
		}

		public FunctionParameterInfo (string name, DisplayText description, MSBuildValueKind type) : base (name, description)
		{
			this.type = string.Join (" ", type.GetTypeDescription ());
		}
	}

	class ClassInfo : BaseSymbol
	{
		public ClassInfo (string name, DisplayText description) : base (name, description)
		{
		}
	}
}