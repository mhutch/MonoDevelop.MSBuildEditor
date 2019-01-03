// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace MonoDevelop.MSBuildEditor.Schema
{
	class FunctionInfo : BaseInfo
	{
		readonly FunctionParameterInfo [] arguments;
		readonly string returnType;

		public virtual string ReturnType => returnType;
		public virtual FunctionParameterInfo [] Parameters => arguments;
		public List<FunctionInfo> Overloads { get; } = new List<FunctionInfo> ();
		public virtual bool IsProperty => false;

		protected FunctionInfo (string name, DisplayText description) : base (name, description)
		{
		}

		public FunctionInfo (string name, DisplayText description, MSBuildValueKind returnType, params FunctionParameterInfo [] arguments) : base (name, description)
		{
			this.arguments = arguments;
			this.returnType = string.Join (" ", returnType.GetTypeDescription());
		}
	}

	class FunctionParameterInfo : BaseInfo
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

	class ClassInfo : BaseInfo
	{
		public ClassInfo (string name, DisplayText description) : base (name, description)
		{
		}
	}
}