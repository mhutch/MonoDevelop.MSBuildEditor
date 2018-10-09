// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace MonoDevelop.MSBuildEditor.Schema
{
	abstract class BaseInfo
	{
		public string Name { get; }
		public DisplayText Description { get; }

		protected BaseInfo (string name, DisplayText description)
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

	abstract class ValueInfo : BaseInfo
	{
		protected ValueInfo (
			string name, DisplayText description, MSBuildValueKind valueKind = MSBuildValueKind.Unknown,
			List<ConstantInfo> values = null, string defaultValue = null)
			: base (name, description)
		{
			Values = values;
			DefaultValue = defaultValue;
			ValueKind = valueKind;
		}

		public MSBuildValueKind ValueKind { get; }
		public List<ConstantInfo> Values { get; }
		public string DefaultValue { get; }
	}
}