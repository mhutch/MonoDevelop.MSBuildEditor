// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using NuGet.Frameworks;

namespace MonoDevelop.MSBuild.Language.Typesystem
{
	// this is kinda weird as it can represent a value that's a piece of a framework ID:
	// a shortname, identifier, version or profile
	// the "name" is the piece that's being represented and the reference is the
	// full ID, or as close to it as we have
	class FrameworkInfo : BaseSymbol, IVersionableSymbol
	{
		public FrameworkInfo (string name, NuGetFramework reference, string? deprecationMessage = null)
			: base (name, null)
		{
			Reference = reference;
			if (deprecationMessage is not null) {
				VersionInfo = SymbolVersionInfo.Deprecated (deprecationMessage);
			}
		}

		public NuGetFramework Reference { get; }

		public SymbolVersionInfo? VersionInfo { get; }
	}
}
