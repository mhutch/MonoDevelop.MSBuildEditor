// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MonoDevelop.MSBuild.Language.Typesystem
{
	public sealed class CustomTypeValue : IDisplayableSymbolOrSyntax, ITypedSymbol
	{
		public CustomTypeValue (string name, DisplayText description)
		{
			Name = name;
			Description = description;
		}

		public CustomTypeInfo CustomType { get; private set; }

		public MSBuildValueKind ValueKind => MSBuildValueKind.CustomType;

		public string Name { get; }

		public DisplayText Description { get; }

		internal void SetParent (CustomTypeInfo parent) => CustomType = parent;
	}
}