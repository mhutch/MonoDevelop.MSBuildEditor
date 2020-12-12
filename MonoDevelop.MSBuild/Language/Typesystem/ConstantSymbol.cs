// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MonoDevelop.MSBuild.Language.Typesystem
{
	/// <summary>
	/// A typed constant ID (but not its value)
	/// </summary>
	public class ConstantSymbol : BaseSymbol
	{
		public ConstantSymbol (string name, DisplayText description, MSBuildValueKind kind) : base (name, description)
		{
			this.ValueKind = kind;
		}

		public MSBuildValueKind ValueKind { get; }
	}
}