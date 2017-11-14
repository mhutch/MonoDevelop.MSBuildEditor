// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. ALl rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace MonoDevelop.MSBuildEditor.Schema
{
	class PropertyInfo : BaseInfo
	{
		public List<ValueInfo> Values { get; }
		public string DefaultValue { get; }
		public char[] ValueSeparators { get; }
		public bool Reserved { get; private set; }
		public bool WellKnown { get; private set; }

		public PropertyInfo (string name, string description, bool wellKnown, bool reserved)
			: base (name, description)
		{
			WellKnown = wellKnown;
			Reserved = reserved;
		}

		public PropertyInfo (string name, string description, bool wellKnown, bool reserved, List<ValueInfo> values, string defaultValue, char[] valueSeparators)
			: this (name, description, wellKnown, reserved)
		{
			Values = values;
			DefaultValue = defaultValue;
			ValueSeparators = valueSeparators;
		}
	}
}