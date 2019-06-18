// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.CodeAnalysis;
using MonoDevelop.MSBuild.Language;

namespace MonoDevelop.MSBuild.Schema
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

		public override DisplayText Description => new DisplayText (Ambience.GetSummaryMarkup (Symbol), true);

		public IMethodSymbol Symbol { get; }
		public override FunctionParameterInfo [] Parameters => Symbol.Parameters.Select (p => new RoslynFunctionArgumentInfo (p)).ToArray ();
		public override MSBuildValueKind ReturnType => RoslynFunctionTypeProvider.ConvertType (Symbol.GetReturnType ());
	}

	class RoslynFunctionArgumentInfo : FunctionParameterInfo
	{
		readonly IParameterSymbol symbol;

		public RoslynFunctionArgumentInfo (IParameterSymbol symbol) : base (symbol.Name, null)
		{
			this.symbol = symbol;
		}

		public override DisplayText Description => new DisplayText (Ambience.GetSummaryMarkup (symbol), true);
		public override string Type => string.Join (" ", RoslynFunctionTypeProvider.ConvertType (symbol.Type).GetTypeDescription ());
	}

	class RoslynClassInfo : ClassInfo
	{
		public RoslynClassInfo (string name, ITypeSymbol symbol) : base (name, null)
		{
			Symbol = symbol;
		}

		public ITypeSymbol Symbol { get; }
		public override DisplayText Description => new DisplayText (Ambience.GetSummaryMarkup (Symbol), true);
	}

	class RoslynPropertyInfo : FunctionInfo
	{
		public RoslynPropertyInfo (IPropertySymbol symbol) : base (symbol.Name, null)
		{
			Symbol = symbol;
		}

		public override DisplayText Description => new DisplayText (Ambience.GetSummaryMarkup (Symbol), true);

		public IPropertySymbol Symbol { get; }
		public override MSBuildValueKind ReturnType => RoslynFunctionTypeProvider.ConvertType (Symbol.GetReturnType ());
		public override FunctionParameterInfo [] Parameters => null;
		public override bool IsProperty => true;
	}
}