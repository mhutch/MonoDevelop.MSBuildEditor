// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. ALl rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace MonoDevelop.MSBuildEditor.Language
{
	class BaseInfo
	{
		public string Name { get; private set; }
		public string Description { get; private set; }

		public BaseInfo (string name, string description)
		{
			Name = name;
			Description = description;
		}

		public override bool Equals (object obj)
		{
			var other = obj as BaseInfo;
			return other != null && string.Equals (Name, other.Name, StringComparison.OrdinalIgnoreCase);
		}

		public override int GetHashCode ()
		{
			return StringComparer.OrdinalIgnoreCase.GetHashCode (Name);
		}
	}
}