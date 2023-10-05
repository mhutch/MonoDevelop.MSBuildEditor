// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MonoDevelop.MSBuild.Language.Typesystem
{
	/// <summary>
	/// Describes a constant's name and type (but not its value)
	/// </summary>
	public class ConstantSymbol : BaseSymbol, ITypedSymbol
	{
		public ConstantSymbol (string name, DisplayText description, MSBuildValueKind kind) : base (name, description)
		{
			this.ValueKind = kind;
		}

		public MSBuildValueKind ValueKind { get; }

		public CustomTypeInfo CustomType => null;
	}
}