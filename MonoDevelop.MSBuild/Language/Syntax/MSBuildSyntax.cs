// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using MonoDevelop.MSBuild.Language.Typesystem;

namespace MonoDevelop.MSBuild.Language.Syntax
{
	public abstract class MSBuildSyntax : IDisplayableSymbolOrSyntax, ITypedSymbol
	{
		protected MSBuildSyntax (
			string name, DisplayText description, MSBuildValueKind valueKind = MSBuildValueKind.Unknown,
			bool isDeprecated = false, string deprecationMessage = null)
		{
			Name = name;
			Description = description;

			IsDeprecated = isDeprecated || !string.IsNullOrEmpty (deprecationMessage);
			DeprecationMessage = deprecationMessage;

			ValueKind = valueKind;
		}

		public string Name { get; }
		public DisplayText Description { get; }

		public MSBuildValueKind ValueKind { get; }
		public CustomTypeInfo CustomType => null;

		public bool IsDeprecated { get; }
		public string DeprecationMessage { get; }

	}
}