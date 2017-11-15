// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace MonoDevelop.MSBuildEditor.Schema
{
	class PropertyInfo : BaseInfo
	{
		public List<ValueInfo> Values { get; }
		public string DefaultValue { get; }
		public char[] ValueSeparators { get; }
		public bool Reserved { get; }
		public bool WellKnown { get; }
		public MSBuildValueKind? ValueKind { get; }

		public PropertyInfo (string name, string description, bool wellKnown, bool reserved)
			: base (name, description)
		{
			WellKnown = wellKnown;
			Reserved = reserved;
		}

		public PropertyInfo (string name, string description, bool wellKnown, bool reserved, MSBuildValueKind? valueKind, List<ValueInfo> values, string defaultValue, char[] valueSeparators)
			: this (name, description, wellKnown, reserved)
		{
			Values = values;
			DefaultValue = defaultValue;
			ValueSeparators = valueSeparators;
			ValueKind = valueKind;
		}
    }
}