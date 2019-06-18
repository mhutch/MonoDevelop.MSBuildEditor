// Copyright (c) 2014 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using MonoDevelop.MSBuild.Schema;

namespace MonoDevelop.MSBuild.Language
{
    interface IFunctionTypeProvider
    {
        IEnumerable<BaseInfo> GetPropertyFunctionNameCompletions(ExpressionNode triggerExpression);
        MSBuildValueKind ResolveType(ExpressionPropertyNode node);
        IEnumerable<FunctionInfo> GetItemFunctionNameCompletions();
        IEnumerable<ClassInfo> GetClassNameCompletions();
        ICollection<FunctionInfo> CollapseOverloads(IEnumerable<FunctionInfo> infos);
        FunctionInfo GetStaticPropertyFunctionInfo(string className, string name);
        FunctionInfo GetPropertyFunctionInfo(MSBuildValueKind valueKind, string name);
        BaseInfo GetItemFunctionInfo(string name);
        BaseInfo GetClassInfo(string name);
        BaseInfo GetEnumInfo(string reference);
    }
}
