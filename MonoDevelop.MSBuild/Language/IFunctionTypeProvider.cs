// Copyright (c) 2014 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Language.Typesystem;

namespace MonoDevelop.MSBuild.Language
{
	interface IFunctionTypeProvider
	{
		IEnumerable<FunctionInfo> GetPropertyFunctionNameCompletions (ExpressionNode triggerExpression);
		MSBuildValueKind ResolveType (ExpressionPropertyNode node);
		IEnumerable<FunctionInfo> GetItemFunctionNameCompletions ();
		IEnumerable<ClassInfo> GetClassNameCompletions ();
		FunctionInfo? GetStaticPropertyFunctionInfo (string className, string name);
		FunctionInfo? GetPropertyFunctionInfo (MSBuildValueKind valueKind, string name);
		FunctionInfo? GetItemFunctionInfo (string name);
		ClassInfo? GetClassInfo (string name);

		//FIXME: this is super broken and needs completely rethinking
		ISymbol? GetEnumInfo (string reference);
		Task EnsureInitialized (CancellationToken token);
	}
}
