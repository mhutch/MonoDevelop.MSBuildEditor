// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.CodeAnalysis;

using MonoDevelop.MSBuild.Language.Typesystem;
using MonoDevelop.MSBuild.Schema;

namespace MonoDevelop.MSBuild.Editor.Roslyn
{
	class RoslynFunctionInfo : FunctionInfo
	{
		static string GetName (IMethodSymbol symbol)
		{
			if (symbol.MethodKind == MethodKind.Constructor)
				return "new";
			return symbol.Name;
		}

		public RoslynFunctionInfo (IMethodSymbol symbol) : base (GetName (symbol), null)
		{
			Symbol = symbol;
		}

		public override DisplayText Description => RoslynHelpers.GetDescription (Symbol);

		public IMethodSymbol Symbol { get; }
		public override FunctionParameterInfo [] Parameters => Symbol.Parameters.Select (p => new RoslynFunctionArgumentInfo (p)).ToArray ();
		public override MSBuildValueKind ReturnType => RoslynFunctionTypeProvider.ConvertType (Symbol.MethodKind == MethodKind.Constructor ? Symbol.ContainingType : Symbol.ReturnType);
	}

	class RoslynFunctionArgumentInfo : FunctionParameterInfo
	{
		readonly IParameterSymbol symbol;

		public RoslynFunctionArgumentInfo (IParameterSymbol symbol) : base (symbol.Name, null)
		{
			this.symbol = symbol;
		}

		public override DisplayText Description => RoslynHelpers.GetDescription (symbol);

		public override string Type => string.Join (" ", RoslynFunctionTypeProvider.ConvertType (symbol.Type).GetTypeDescription ());
	}

	class RoslynClassInfo : ClassInfo
	{
		public RoslynClassInfo (string name, ITypeSymbol symbol) : base (name, null)
		{
			Symbol = symbol;
		}

		public ITypeSymbol Symbol { get; }

		public override DisplayText Description => RoslynHelpers.GetDescription (Symbol);
	}

	class RoslynPropertyInfo : FunctionInfo
	{
		// is this is a property on an array, contains the array element type
		readonly MSBuildValueKind? arrayElementType;

		public RoslynPropertyInfo (IPropertySymbol symbol, MSBuildValueKind? arrayElementType) : base (symbol.Name, null)
		{
			Symbol = symbol;
			this.arrayElementType = arrayElementType;
		}

		public override DisplayText Description => RoslynHelpers.GetDescription (Symbol);

		public IPropertySymbol Symbol { get; }
		public override MSBuildValueKind ReturnType => arrayElementType ?? RoslynFunctionTypeProvider.ConvertType (Symbol.Type);
		public override FunctionParameterInfo [] Parameters => null;
		public override bool IsProperty => true;
	}
}