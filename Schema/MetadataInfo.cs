// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using MonoDevelop.MSBuildEditor.Language;

namespace MonoDevelop.MSBuildEditor.Schema
{
	class MetadataInfo : BaseInfo
	{
		public List<ValueInfo> Values { get; }
		public string DefaultValue { get; }
		public char[] ValueSeparators { get; }
		public bool WellKnown { get; }
		public bool Required { get; }

		public MetadataInfo (string name, string description, bool wellKnown)
			: base (name, description)
		{
			WellKnown = wellKnown;
		}

		public MetadataInfo (string name, string description, bool wellKnown, bool required, List<ValueInfo> values, string defaultValue, char[] valueSeparators)
			: this (name, description, wellKnown)
		{
			Values = values;
			DefaultValue = defaultValue;
			ValueSeparators = valueSeparators;
			Required = required;
		}

		public override MSBuildKind Kind => MSBuildKind.Metadata;
    }
}