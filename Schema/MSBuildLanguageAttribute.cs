// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace MonoDevelop.MSBuildEditor.Schema
{
	class MSBuildLanguageAttribute : BaseInfo
	{
		public MSBuildLanguageAttribute (string name, string description, MSBuildValueKind valueKind, bool required = false, bool isAbstract = false)
			: base (name, description)
		{
			ValueKind = valueKind;
			Required = required;
			IsAbstract = isAbstract;
		}

		public MSBuildValueKind ValueKind { get; }
		public bool Required { get; }
		public bool IsAbstract { get; }
    }
}
