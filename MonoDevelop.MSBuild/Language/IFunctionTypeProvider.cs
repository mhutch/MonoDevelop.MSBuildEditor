// Copyright (c) 2014 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.MSBuild.Language.Typesystem;

namespace MonoDevelop.MSBuild.Language
{
	interface IFunctionTypeProvider
	{
		IEnumerable<BaseSymbol> GetPropertyFunctionNameCompletions (ExpressionNode triggerExpression);
		MSBuildValueKind ResolveType (ExpressionPropertyNode node);
		IEnumerable<FunctionInfo> GetItemFunctionNameCompletions ();
		IEnumerable<ClassInfo> GetClassNameCompletions ();
		ICollection<FunctionInfo> CollapseOverloads (IEnumerable<FunctionInfo> infos);
		FunctionInfo GetStaticPropertyFunctionInfo (string className, string name);
		FunctionInfo GetPropertyFunctionInfo (MSBuildValueKind valueKind, string name);
		BaseSymbol GetItemFunctionInfo (string name);
		BaseSymbol GetClassInfo (string name);
		BaseSymbol GetEnumInfo (string reference);
		Task EnsureInitialized (CancellationToken token);
	}
}
