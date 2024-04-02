// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.MSBuild.Language.Typesystem;

namespace MonoDevelop.MSBuild.Language;

record class KnownCulture(string Name, string DisplayName, int Lcid)
{
	// 4096 is the "not found" lcid
	public bool HasKnownLcid => Lcid != 4096;

	public ITypedSymbol CreateLcidSymbol () => new ConstantSymbol (Lcid.ToString (), $"The LCID of the {DisplayName} culture", MSBuildValueKind.Lcid);

	public ITypedSymbol CreateCultureSymbol () => new ConstantSymbol (Name, $"The name of the {DisplayName} culture", MSBuildValueKind.Culture);
}
