// Copyright (c) 2016 Xamarin Inc.
// Copyright (c) Microsoft. ALl rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MonoDevelop.MSBuildEditor.Language
{
	class MetadataInfo : BaseInfo
	{
		public bool WellKnown { get; private set; }

		public MetadataInfo (string name, string description, bool wellKnown = false)
			: base (name, description)
		{
			WellKnown = wellKnown;
		}
	}
}