// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace MonoDevelop.MSBuild.Language.Typesystem
{
	/// <summary>
	/// Anything in the type system with a name and description
	/// </summary>
	public abstract class BaseSymbol : IDisplayableSymbolOrSyntax

	{
		readonly DisplayText description;

		public string Name { get; }
		public virtual DisplayText Description => description;

		protected BaseSymbol (string name, DisplayText description)
		{
			Name = name;
			this.description = description;
		}

		public override bool Equals (object obj)
		{
			var other = obj as BaseSymbol;
			return other != null && string.Equals (Name, other.Name, StringComparison.OrdinalIgnoreCase);
		}

		public override int GetHashCode ()
		{
			return StringComparer.OrdinalIgnoreCase.GetHashCode (Name);
		}
	}
}