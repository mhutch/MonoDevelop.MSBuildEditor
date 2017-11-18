// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace MonoDevelop.MSBuildEditor.Schema
{
	class PropertyInfo : VariableInfo
	{
		public bool Reserved { get; }
		public bool WellKnown { get; }

		public PropertyInfo (
			string name, string description, bool wellKnown, bool reserved,
			MSBuildValueKind valueKind = MSBuildValueKind.Unknown,
			List<ConstantInfo> values = null, string defaultValue = null, char[] valueSeparators = null)
			: base (name, description, valueKind, values, defaultValue, valueSeparators)
		{
			WellKnown = wellKnown;
			Reserved = reserved;
		}
    }
}