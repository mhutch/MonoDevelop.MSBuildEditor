// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace MonoDevelop.MSBuild.Language.Typesystem;

public abstract class VariableInfo : BaseSymbol, ITypedSymbol, IVersionableSymbol
{
	protected VariableInfo (
		string name, DisplayText description, MSBuildValueKind valueKind = MSBuildValueKind.Unknown,
		CustomTypeInfo? customType = null, string? defaultValue = null, SymbolVersionInfo? versionInfo = null)
		: base (name, description)
	{
		if (valueKind.IsCustomType () && customType == null) {
			throw new ArgumentException ($"When {nameof(valueKind)} is {nameof(MSBuildValueKind.CustomType)}, {nameof (customType)} cannot be null");
		}

		// HACK: NuGetID stores PackageType on the CustomType
		if (customType != null && !(valueKind.IsCustomType () || valueKind.IsKindOrListOfKind (MSBuildValueKind.NuGetID))) {
			throw new ArgumentException ($"When {nameof(customType)} is provided, {nameof(valueKind)} must be {nameof(MSBuildValueKind.CustomType)}");
		}

		CustomType = customType;
		DefaultValue = defaultValue;
		VersionInfo = versionInfo;
		ValueKind = valueKind;
	}

	public MSBuildValueKind ValueKind { get; }
	public CustomTypeInfo? CustomType { get; }
	public string? DefaultValue { get; }
	public SymbolVersionInfo? VersionInfo { get; }
}