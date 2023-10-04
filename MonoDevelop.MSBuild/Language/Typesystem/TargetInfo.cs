// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MonoDevelop.MSBuild.Language.Typesystem
{
	class TargetInfo : BaseSymbol, IDeprecatable
	{
		public TargetInfo (string name, DisplayText description, bool isDeprecated = false, string? deprecationMessage = null)
			: base (name, description)
		{
			IsDeprecated = isDeprecated || !string.IsNullOrEmpty (deprecationMessage);
			DeprecationMessage = deprecationMessage;
		}

		public bool IsDeprecated { get; }

		public string DeprecationMessage { get; }
	}
}