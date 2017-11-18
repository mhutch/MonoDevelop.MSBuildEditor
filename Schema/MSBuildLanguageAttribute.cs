// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace MonoDevelop.MSBuildEditor.Schema
{
	class MSBuildLanguageAttribute : VariableInfo
	{
		public MSBuildLanguageAttribute (
			string name, string description, MSBuildValueKind valueKind,
			bool required = false, MSBuildKind? abstractKind = null)
			: base (name, description, valueKind)
		{
			Required = required;
			AbstractKind = abstractKind;
		}

		public MSBuildKind? AbstractKind { get; }
		public bool Required { get; }
		public bool IsAbstract => AbstractKind.HasValue;
    }
}
