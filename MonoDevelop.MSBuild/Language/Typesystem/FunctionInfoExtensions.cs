// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;

namespace MonoDevelop.MSBuild.Language.Typesystem;

static class FunctionInfoExtensions
{
	/// <summary>
	/// If there are multiple <see cref="FunctionInfo"/> items that have the same name, collapse them into a single <see cref="FunctionInfo"/> with overloads.
	/// </summary>
	public static ICollection<FunctionInfo> CollapseOverloads (this IEnumerable<FunctionInfo> infos)
	{
		var functions = new Dictionary<string, FunctionInfo> ();
		foreach (var info in infos) {
			if (functions.TryGetValue (info.Name, out FunctionInfo existing)) {
				existing.Overloads.Add (info);
			} else {
				functions.Add (info.Name, info);
			}
		}
		return functions.Values.ToArray ();
	}
}
