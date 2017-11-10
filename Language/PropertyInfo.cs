// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. ALl rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MonoDevelop.MSBuildEditor.Language
{
	class PropertyInfo : BaseInfo
	{
		public bool Reserved { get; private set; }
		public bool WellKnown { get; private set; }

		public PropertyInfo (string name, string description, bool wellKnown = false, bool reserved = false)
			: base (name, description)
		{
			WellKnown = wellKnown;
			Reserved = reserved;
		}
	}
}